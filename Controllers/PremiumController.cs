using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Project.Models;
using Web_Project.Services.Premium;

namespace Web_Project.Controllers
{
    [ApiController]
    [Authorize(Policy = "UserOnly")]
    [Route("api/premium")]
    public class PremiumController : ControllerBase
    {
        private readonly IUserTokenQuotaService _tokenQuotaService;
        private readonly IPremiumPaymentService _paymentService;
        private readonly IPremiumPlanSettingsService _planSettingsService;

        public PremiumController(
            IUserTokenQuotaService tokenQuotaService,
            IPremiumPaymentService paymentService,
            IPremiumPlanSettingsService planSettingsService)
        {
            _tokenQuotaService = tokenQuotaService;
            _paymentService = paymentService;
            _planSettingsService = planSettingsService;
        }

        [AllowAnonymous]
        [HttpGet("plan")]
        public async Task<ActionResult> GetPlan(CancellationToken cancellationToken)
        {
            var settings = await _planSettingsService.GetSettingsAsync(cancellationToken);

            return Ok(new
            {
                planCode = "PREMIUM_30D",
                planName = "SynapLearn Premium",
                amount = settings.Amount,
                currency = "VND",
                days = settings.Days,
                dailyTokenLimit = 500000
            });
        }

        [HttpGet("status")]
        public async Task<ActionResult<PremiumStatusResponse>> GetStatus(CancellationToken cancellationToken)
        {
            var userId = ResolveUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });
            }

            return Ok(await _tokenQuotaService.GetStatusAsync(userId.Value, cancellationToken));
        }

        [HttpPost("checkout")]
        public async Task<ActionResult<CheckoutResponse>> CreateCheckout(
            [FromBody] CheckoutRequest? request,
            CancellationToken cancellationToken)
        {
            var userId = ResolveUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập để thanh toán Premium." });
            }

            try
            {
                var response = await _paymentService.CreateCheckoutAsync(
                    userId.Value,
                    request?.PlanName ?? "Premium",
                    Request.Scheme,
                    Request.Host.ToUriComponent(),
                    cancellationToken);

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("payments/{transactionId:int}/success")]
        public async Task<ActionResult<PaymentTransactionResponse>> MarkPaymentSuccess(
            int transactionId,
            CancellationToken cancellationToken)
        {
            var userId = ResolveUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });
            }

            try
            {
                return Ok(await _paymentService.MarkPaymentSuccessAsync(userId.Value, transactionId, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("payments/{transactionId:int}/failed")]
        public async Task<ActionResult<PaymentTransactionResponse>> MarkPaymentFailed(
            int transactionId,
            [FromQuery] string status,
            CancellationToken cancellationToken)
        {
            var userId = ResolveUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { message = "Phiên đăng nhập không hợp lệ." });
            }

            try
            {
                return Ok(await _paymentService.MarkPaymentFailedAsync(userId.Value, transactionId, status, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private int? ResolveUserId()
        {
            var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdRaw, out var userId) ? userId : null;
        }
    }
}

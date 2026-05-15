using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Web_Project.Models.Dtos.Payments;
using Web_Project.Services.Payments;

namespace Web_Project.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly IMoMoPaymentService _moMoPaymentService;
        private readonly IPremiumSubscriptionService _premiumSubscriptionService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            IMoMoPaymentService moMoPaymentService,
            IPremiumSubscriptionService premiumSubscriptionService,
            ILogger<PaymentsController> logger)
        {
            _moMoPaymentService = moMoPaymentService;
            _premiumSubscriptionService = premiumSubscriptionService;
            _logger = logger;
        }

        [Authorize(Policy = "UserOnly")]
        [HttpGet("premium/status")]
        public async Task<ActionResult<SubscriptionStatusResponse>> GetPremiumStatus(CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var status = await _premiumSubscriptionService.GetStatusAsync(userId, cancellationToken);
            return Ok(new SubscriptionStatusResponse
            {
                IsPremium = status.IsPremium,
                PlanCode = status.PlanCode,
                ExpiresAt = status.ExpiresAt
            });
        }

        [Authorize(Policy = "UserOnly")]
        [HttpPost("momo/create")]
        public async Task<ActionResult<CreateMoMoPaymentResponse>> CreateMoMoPayment(
            [FromBody] CreateMoMoPaymentRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _moMoPaymentService.CreatePaymentAsync(
                userId,
                request ?? new CreateMoMoPaymentRequest(),
                Request,
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("momo/return")]
        public async Task<IActionResult> MoMoReturn(
            [FromQuery] MoMoPaymentResult payload,
            CancellationToken cancellationToken)
        {
            if (payload.ResultCode == 0 && !string.IsNullOrWhiteSpace(payload.Signature))
            {
                var handleResult = await _moMoPaymentService.HandleSignedReturnAsync(payload, cancellationToken);
                if (!handleResult.Accepted)
                {
                    _logger.LogWarning("MoMo signed return was not applied: {Message}", handleResult.Message);
                }
            }

            var isSuccess = payload.ResultCode == 0;
            var query = new QueryString()
                .Add("payment", isSuccess ? "success" : "failed")
                .Add("provider", "momo")
                .Add("orderId", payload.OrderId ?? string.Empty)
                .Add("requestId", payload.RequestId ?? string.Empty)
                .Add("message", payload.Message ?? string.Empty);

            return Redirect(isSuccess
                ? $"/premium/payment-success.html{query}"
                : $"/premium/payment-failed.html{query}");
        }

        [AllowAnonymous]
        [HttpPost("momo/ipn")]
        public async Task<IActionResult> MoMoIpn(
            [FromBody] MoMoPaymentResult payload,
            CancellationToken cancellationToken)
        {
            var result = await _moMoPaymentService.HandleIpnAsync(payload, cancellationToken);
            if (!result.Accepted)
            {
                _logger.LogWarning("MoMo IPN rejected: {Message}", result.Message);
            }

            return NoContent();
        }

        private bool TryGetUserId(out int userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out userId);
        }
    }
}

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
        private readonly IPayOSPaymentService _payOSPaymentService;
        private readonly IPremiumSubscriptionService _premiumSubscriptionService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            IPayOSPaymentService payOSPaymentService,
            IPremiumSubscriptionService premiumSubscriptionService,
            ILogger<PaymentsController> logger)
        {
            _payOSPaymentService = payOSPaymentService;
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
        [HttpPost("payos/create")]
        public async Task<ActionResult<CreatePayOSPaymentResponse>> CreatePayOSPayment(
            [FromBody] CreatePayOSPaymentRequest request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var result = await _payOSPaymentService.CreatePaymentAsync(
                userId,
                request ?? new CreatePayOSPaymentRequest(),
                Request,
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("payos/return")]
        public IActionResult PayOSReturn([FromQuery] long? orderCode)
        {
            var query = new QueryString()
                .Add("payment", "success")
                .Add("orderId", orderCode?.ToString() ?? string.Empty)
                .Add("orderCode", orderCode?.ToString() ?? string.Empty)
                .Add("message", "PayOS đã ghi nhận thao tác thanh toán. Hệ thống sẽ mở Premium sau khi webhook được xác nhận.");

            return Redirect($"/premium/payment-success.html{query}");
        }

        [AllowAnonymous]
        [HttpGet("payos/cancel")]
        public IActionResult PayOSCancel([FromQuery] long? orderCode)
        {
            var query = new QueryString()
                .Add("payment", "failed")
                .Add("orderId", orderCode?.ToString() ?? string.Empty)
                .Add("orderCode", orderCode?.ToString() ?? string.Empty)
                .Add("message", "Bạn đã huỷ thanh toán PayOS.");

            return Redirect($"/premium/payment-failed.html{query}");
        }

        [AllowAnonymous]
        [HttpPost("payos/webhook")]
        public async Task<IActionResult> PayOSWebhook(
            [FromBody] PayOSWebhookPayload payload,
            CancellationToken cancellationToken)
        {
            var result = await _payOSPaymentService.HandleWebhookAsync(payload, cancellationToken);
            if (!result.Accepted)
            {
                _logger.LogWarning("PayOS webhook rejected: {Message}", result.Message);
            }

            return Ok(new { success = true });
        }

        private bool TryGetUserId(out int userId)
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out userId);
        }
    }
}

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
        public async Task<IActionResult> PayOSReturn(
            [FromQuery] string? orderCode,
            [FromQuery] string? id,
            [FromQuery] string? status,
            [FromQuery] bool? cancel,
            CancellationToken cancellationToken)
        {
            if (cancel == true)
            {
                return Redirect("/premium/checkout.html?payment=cancelled");
            }

            var result = await _payOSPaymentService.HandleReturnAsync(orderCode, id, cancellationToken);
            if (result.Paid)
            {
                return Redirect("/premium/dashboard.html?payment=success");
            }

            _logger.LogWarning(
                "PayOS return was not applied. OrderCode={OrderCode}; PaymentLinkId={PaymentLinkId}; Status={Status}; Message={Message}",
                orderCode,
                id,
                status,
                result.Message);
            return Redirect("/premium/checkout.html?payment=pending");
        }

        [AllowAnonymous]
        [HttpGet("payos/cancel")]
        public IActionResult PayOSCancel(
            [FromQuery] string? orderCode,
            [FromQuery] string? id)
        {
            return Redirect("/premium/checkout.html?payment=cancelled");
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

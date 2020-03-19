using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Text.Json;
using ThreeDPayment.Sample.Models;

namespace ThreeDPayment.Controllers
{
    public class PaymentController : Controller
    {
        private const string PaymentSessionName = "PaymentInfo";
        private const string PaymentResultSessionName = "PaymentResult";

        private readonly IHtmlHelper _htmlHelper;
        private readonly IPaymentProviderFactory _paymentProviderFactory;

        public PaymentController(IHtmlHelper htmlHelper,
            IPaymentProviderFactory paymentProviderFactory)
        {
            _htmlHelper = htmlHelper;
            _paymentProviderFactory = paymentProviderFactory;
        }

        public IActionResult Index()
        {
            var model = new PaymentViewModel
            {
                Banks = _htmlHelper.GetEnumSelectList<Banks>().ToList()
            };
            model.Banks.Insert(0, new SelectListItem("Seçiniz", string.Empty));

            return View(model);
        }

        [HttpPost]
        public IActionResult Index(PaymentViewModel model)
        {
            if (ModelState.IsValid)
            {
                HttpContext.Session.Set(PaymentSessionName, JsonSerializer.SerializeToUtf8Bytes(model));
                return RedirectToAction(nameof(ThreeDGate));
            }

            ModelState.AddModelError(string.Empty, "Lütfen bilgileri kontrol edin.");
            return View(model);
        }

        public IActionResult ThreeDGate()
        {
            HttpContext.Session.TryGetValue(PaymentSessionName, out byte[] paymentInfo);
            if (paymentInfo==null)
                return RedirectToAction(nameof(Index));

            var paymentModel = JsonSerializer.Deserialize<PaymentViewModel>(paymentInfo);
            if (paymentModel == null)
                return RedirectToAction(nameof(Index));

            var paymentProvider = _paymentProviderFactory.Create(paymentModel.SelectedBank);
            var paymentParameterResult = paymentProvider.GetPaymentParameters(new PaymentRequest
            {
                CardHolderName = paymentModel.CardHolderName,
                CardNumber = paymentModel.CardNumber,
                ExpireMonth = paymentModel.ExpireMonth,
                ExpireYear = paymentModel.ExpireYear,
                CvvCode = paymentModel.CvvCode,
                Installment = paymentModel.Installment,
                TotalAmount = 1.00m,
                CustomerIpAddress = HttpContext.Connection.RemoteIpAddress.ToString(),
                CurrencyIsoCode = "949",
                LanguageIsoCode = "tr",
                OrderNumber = Guid.NewGuid().ToString()
            });

            var paymentForm = _paymentProviderFactory.CreatePaymentForm(paymentParameterResult.Parameters, paymentParameterResult.PaymentUrl);
            return View(model: paymentForm);
        }

        public IActionResult Callback(IFormCollection form)
        {
            if (HttpContext.Session.TryGetValue(PaymentSessionName, out byte[] paymentInfo))
                return RedirectToAction(nameof(Index));

            var paymentModel = JsonSerializer.Deserialize<PaymentViewModel>(paymentInfo);
            if (paymentModel == null)
                return RedirectToAction(nameof(Index));

            //clear payment info session
            HttpContext.Session.Remove(PaymentSessionName);

            var paymentProvider = _paymentProviderFactory.Create(paymentModel.SelectedBank);
            var paymentResult = paymentProvider.GetPaymentResult(form);
            HttpContext.Session.Set(PaymentResultSessionName, JsonSerializer.SerializeToUtf8Bytes(paymentResult));

            if (paymentResult.Success)
            {
                return RedirectToAction(nameof(Success));
            }

            return RedirectToAction(nameof(Fail));
        }

        public IActionResult Success()
        {
            if (HttpContext.Session.TryGetValue(PaymentResultSessionName, out byte[] result))
                return RedirectToAction(nameof(Index));

            var paymentResult = JsonSerializer.Deserialize<PaymentResult>(result);
            if (paymentResult == null)
                return RedirectToAction(nameof(Index));

            return View(paymentResult);
        }

        public IActionResult Fail()
        {
            if (HttpContext.Session.TryGetValue(PaymentResultSessionName, out byte[] result))
                return RedirectToAction(nameof(Index));

            var paymentResult = JsonSerializer.Deserialize<PaymentResult>(result);
            if (paymentResult == null)
                return RedirectToAction(nameof(Index));

            return View(paymentResult);
        }
    }
}

using CtoRiscVMvc.Models;
using CtoRiscVMvc.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CtoRiscVMvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Ýlk açýlýþta boþ model gönderiyoruz
            return View(new ConversionModel());
        }

        [HttpPost]
        public IActionResult Index(ConversionModel model)
        {
            if (!string.IsNullOrEmpty(model.InputCCode))
            {
                try
                {
                    // Kullanýcýdan gelen C kodunu döngü bazlý RISC-V koduna çeviriyoruz
                    model.OutputRiscVCode = LoopConverter.ConvertLoops(model.InputCCode);
                }
                catch (Exception ex)
                {
                    // Hata varsa logla ve kullanýcýya göster
                    _logger.LogError(ex, "Dönüþüm sýrasýnda hata oluþtu.");
                    ModelState.AddModelError(string.Empty, "C kodu dönüþtürülürken bir hata oluþtu. Lütfen kodunuzu kontrol edin.");
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Lütfen C kodunuzu giriniz.");
            }

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

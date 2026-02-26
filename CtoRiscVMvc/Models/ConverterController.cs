using Microsoft.AspNetCore.Mvc;
using CtoRiscVMvc.Models;
using CtoRiscVMvc.Services;

namespace CtoRiscVMvc.Controllers
{
    public class ConverterController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Index(ConversionModel model)
        {
            if (!string.IsNullOrEmpty(model.InputCCode))
            {
                model.OutputRiscVCode = LoopConverter.ConvertLoops(model.InputCCode);
            }
            return View(model);
        }
    }
}

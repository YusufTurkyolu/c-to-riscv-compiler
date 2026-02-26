using System.ComponentModel.DataAnnotations;

namespace CtoRiscVMvc.Models
{
    /// Kullanıcıdan alınan C kodu ve dönüştürülen RISC-V kodunu tutar.
    public class ConversionModel
    {
        
        /// Kullanıcının girdiği C dilinde kod (döngüler içerebilir).
       
        [Required(ErrorMessage = "Lütfen C kodunuzu giriniz.")]
        public string InputCCode { get; set; }

        /// Dönüşüm sonucu oluşturulan RISC-V assembly kodu.
        public string OutputRiscVCode { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Glasswall.CloudProxy.Common.Web.Models
{
    public class Base64Request
    {
        [Required]
        public string Base64 { get; set; }
    }
}

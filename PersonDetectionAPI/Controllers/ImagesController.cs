using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PersonDetectionAPI.Handlers;

namespace PersonDetectionAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        private readonly IImageHandler _imageHandler;

        public ImagesController(IImageHandler imageHandler)
        {
            _imageHandler = imageHandler;
        }
        
        [HttpPost]
        [Route("uploadImage")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            return await _imageHandler.UploadImage(file);
        }
    }
}

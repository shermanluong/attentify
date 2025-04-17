using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tesseract;

namespace GoogleLogin.Services
{
    public class OcrService
    {
		private const string TesseractDataPath = @"C:\Program Files\Tesseract-OCR\tessdata"; // Adjust this path to your Tesseract data folder

		public static string ExtractTextFromImage(byte[] imageData)
		{
			try
			{
				using (var image = Image.Load(imageData))
				{
					// Convert ImageSharp image to Pix (Tesseract format)
					using (var pix = Pix.LoadFromMemory(imageData))
					{
						// Initialize Tesseract OCR engine
						using (var ocr = new TesseractEngine(TesseractDataPath, "eng", EngineMode.Default))
						{
							// Process the image
							var result = ocr.Process(pix);
							return result.GetText();  // Return the recognized text
						}
					}
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine("ocrservice/extracttextfromimage " + ex.Message);
			}
			return "";
		}		
    }
}
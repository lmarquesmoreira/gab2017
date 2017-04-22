#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
#r "System.Drawing"

using System.Net;
using System.Net.Http; 
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;
using System.IO; 
using System.Drawing;
using System.Drawing.Imaging;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log) {
    var image = await req.Content.ReadAsStreamAsync();
    
    if(image == null){
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }
    
    log.Info("Copiando a imagem");
    
    // para fins da demo... 
    MemoryStream mem = new MemoryStream();
    image.CopyTo(mem);
    image.Position = 0;
    mem.Position = 0;

    string result = await CallVisionAPI(image);
    log.Info(result); 
 
    if (String.IsNullOrEmpty(result))
    {
        return req.CreateResponse(HttpStatusCode.BadRequest);
    }
 
    List<FaceData> imageData = JsonConvert.DeserializeObject<List<FaceData>>(result);
 
    log.Info("Escrevendo o retangulo na imagem, se houver");
    MemoryStream outputStream = new MemoryStream();
    using(Image maybeFace = Image.FromStream(mem, true))
    {
        using (Graphics g = Graphics.FromImage(maybeFace))
        {
            Pen yellowPen = new Pen(Color.Yellow, 4);
            foreach (var face in imageData)
            {
                var faceRectangle = face.faceRectangle;
                g.DrawRectangle(yellowPen, 
                    faceRectangle.left, faceRectangle.top, 
                    faceRectangle.width, faceRectangle.height);
            }
        }
        maybeFace.Save(outputStream, ImageFormat.Jpeg);
    }
     
    var response = new HttpResponseMessage()
    {
        Content = new ByteArrayContent(outputStream.ToArray()),
        StatusCode = HttpStatusCode.OK,
    };
    response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
    return response; 
}
 
static async Task<string> CallVisionAPI(Stream image)
{
    using (var client = new HttpClient())
    {
        var content = new StreamContent(image);
        var url = "https://eastus2.api.cognitive.microsoft.com/face/v1.0/detect?returnFaceId=true&returnFaceLandmarks=false";
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Environment.GetEnvironmentVariable("VISION_API_KEY"));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var httpResponse = await client.PostAsync(url, content);
 
        if (httpResponse.StatusCode == HttpStatusCode.OK){
            return await httpResponse.Content.ReadAsStringAsync();
        }
    }
    return null;
}

public class FaceRectangle
{
    public int top { get; set; }
    public int left { get; set; }
    public int width { get; set; }
    public int height { get; set; }
}

public class FaceData
{
    public string faceId { get; set; }
    public FaceRectangle faceRectangle { get; set; }
}
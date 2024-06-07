using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using WhatsappChatBot.Models;


namespace WhatsappChatBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WhatsAppController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        WhatsAppDbContext context;
        public WhatsAppDbContext _context { get { return context; } }

        public WhatsAppController(IConfiguration configuration)
        {
            _configuration = configuration;
            context = new WhatsAppDbContext(_configuration);
        }

        [HttpPost, HttpGet]
        public async Task<IActionResult> PostAsync() 
        {
            System.Console.WriteLine(Request);
            var req = Request;
            try
            {
                //validate the webhook
                string mode = req.Query["hub.node"];
                string challenge = req.Query["hub.challenge"];
                string verifyToken = req.Query["hub.verify_token"];

                if (!string.IsNullOrEmpty(mode) && !string.IsNullOrEmpty(verifyToken))
                {
                    if (mode == "subscribe" && verifyToken == "hello from dasun")
                    {
                        System.Console.WriteLine("Token Verified");
                        string responseMessage = challenge;
                        return new OkObjectResult(responseMessage);
                    }
                    else
                    {
                        System.Console.WriteLine("Error Validation");
                        return new BadRequestObjectResult("Your error message");
                    }
                }
                else
                {
                    //handle incoming message
                    string requestBody = await new StreamReader(Request.Body).ReadToEndAsync();
                    var receivedMessage = JObject.Parse(requestBody);
                    string senderPhoneNumber = receivedMessage["entry"][0]["changes"][0]["value"]["messages"][0]["from"].ToString();
                    string senderId = receivedMessage["entry"][0]["changes"][0]["value"]["metadata"]["phone_number_id"].ToString();
                    string incomingMessageText = receivedMessage["entry"][0]["changes"][0]["value"]["messages"][0]["text"]["body"].ToString();

                    //create Response Async
                    JObject response = new JObject();
                    response["messaging_product"] = "whatsapp";
                    response["to"] = senderPhoneNumber;

                    JObject message = new JObject();

                    //get employee name from db
                    var employee = _context.Employees.Where(e => e.Id.ToString() ==
                        incomingMessageText).FirstOrDefault();

                    message["body"] = employee.Name + " -> " + employee.DepartmentName;
                    response["text"] = message;
                    await SendMessageAsync(response, senderId);

                    return Ok();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest();
            }
            
        }

        private async Task SendMessageAsync(JObject message, string senderId)
        {
            string apiUrl = $"https://graph.facebook.com/v16.0/{senderId}/messages";
            var apiToken = _configuration["MetaDeveloper:APIToken"];

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

                var content = new StringContent(message.ToString(), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Message sent successfully.");
                }
                else
                {
                    Console.WriteLine($"Error sending message: {response.StatusCode}");
                }
            }
        }
    }
}

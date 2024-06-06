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


namespace WhatsappChatBot.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WhatsAppController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IstorageHelper _storageHelper;

        public WhatsAppController(IConfiguration configuration, IstorageHelper storageHelper)
        {
            _configuration = configuration;
            _storageHelper = storageHelper;
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

                    //new implementation
                    ChatContext chatContext = null;

                    if (incomingMessageText.ToLower().Equals("new"))
                    {
                        chatContext = new ChatContext()
                        {
                            Model = _configuration["OpenAi:Model"],
                            Messages = new List<Message>()
                            {
                                new Message()
                                {
                                    Role = "user",
                                    Content = "hi"
                                }
                            }
                        };
                    }
                    else
                    {
                        var chatContextEntity = await _storageHelper.GetEntityAsync<GptResponseEntity>(_configuration
                            ["StorageAccount:GPTContextTable"], "WhatsAppConversation", senderPhoneNumber);

                        if(chatContextEntity != null)
                        {
                            chatContext = new ChatContext();
                            chatContext.Messages = JsonConvert.DeserializeObject<List<Messages>>(chatContextEntity.UserContext);
                            chatContext.Model = _configuration["OpenAI:Model"];
                            chatContext.Messages.Add(new Message()
                            {
                                Role = "user",
                                Content = incomingMessageText
                            });
                        }
                        else
                        {
                            chatContext = new ChatContext()
                            {
                                Model = _configuration["OpenAI:Model"],
                                Messages = new List<Message>()
                                {
                                    new Message()
                                    {
                                        RoleManager = "user",
                                        Content = incomingMessageText
                                    }
                                }
                            };
                        }
                    }

                    //create Response Async
                    JObject response = new JObject();
                    response["messaging_product"] = "whatsapp";
                    response["to"] = senderPhoneNumber;

                    JObject message = new JObject();
                    Message gptResponse = await GetGPTResponse(incomingMessageText, chatContext);

                    if (gptResponse != null)
                    {
                        message["body"] = gptResponse.Content;
                        response["text"] = message;
                        await SendMessageAsync(response, senderId);

                        chatContext.Messages.Add(gptResponse);
                        await _storageHelper.InsertEntityAsync(_configuration["StorageAccount:GPTContextTable"], 
                            new GptResponseEntity()
                            {
                                PartitionKey = "WhatsAppConversation",
                                RowKey = senderPhoneNumber,
                                UserContext = JsonConvert.SerializeObject(chatContext.Messages)
                            });
                    }
                    else
                    {
                        message["body"] = "Sorry, I didn't understand that.";
                        response["text"] = message;
                        await SendMessageAsync(response, senderId);
                    }
                    return Ok();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest();
            }
            
        }

        private async Task<Message> GetGPTResponse(string text, object jsonBody)
        {
            //call an api with a POST request and json body with headers
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(_configuration["OpenAI:APIEndpoint"]);
            client.DefaultRequestHeaders.Accept.Clear();

            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _configuration["OpenAI:APIKey"]);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, client.BaseAddress);

            request.Content = new StringContent(JsonConvert.SerializeObject(jsonBody), Encoding.UTF8, "applecation/json");
            var response = await client.SendAsync(request).ConfigureAwait(false);
            var responseString = string.Empty;
            try
            {
                response.EnsureSuccessStatusCode();
                responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseJson = JObject.Parse(responseString);
                return JsonConvert.DeserializeObject<Message>(responseJson["choices"][0]["message"].ToString());
            }
            catch (HttpRequestException ex)
            {
                await Console.Out.WriteLineAsync(ex.Message);
                return null;
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

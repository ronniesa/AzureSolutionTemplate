
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopicPropertyMiddleware
{
    /// <summary>
    /// Function that receives messages from Stream Analytics and sends them to a Service Bus topic adding custom properties to enable filtering by consumers
    /// </summary>
    public static class TopicWriter
    {
        const string SERVICE_BUS_PROPERTY_NAME = "SERVICE_BUS";
        const string SERVICE_BUS_TOPIC_PROPERTY_NAME = "SERVICE_BUS_TOPIC";
        const string MESSAGE_PROPERTIES_PROPERTY_NAME = "MESSAGE_PROPERTIES";

        static IList<PropertyMapping> properties;
        static TopicClient topicClient;

        static TopicWriter()
        {
            // Resolving the properties once at startup
            var propertiesRaw = System.Environment.GetEnvironmentVariable(MESSAGE_PROPERTIES_PROPERTY_NAME);
            if (!string.IsNullOrEmpty(propertiesRaw))
                properties = PropertyMapping.Parse(propertiesRaw);

            var serviceBusConnectionString = System.Environment.GetEnvironmentVariable(SERVICE_BUS_PROPERTY_NAME);
            var topicName = System.Environment.GetEnvironmentVariable(SERVICE_BUS_TOPIC_PROPERTY_NAME);
            if (!string.IsNullOrEmpty(serviceBusConnectionString) && !string.IsNullOrEmpty(topicName))
                topicClient = new TopicClient(new ServiceBusConnectionStringBuilder(serviceBusConnectionString));

        }

        /// <summary>
        /// Writes the messages to a topic extracting user properties to filter
        /// Parameters to be defined in Azure Functions properties
        /// - SERVICE_BUS: contains the Service Bus Connection String
        /// - SERVICE_BUS_TOPIC: The name of the Service Bus Topic
        /// - MESSAGE_PROPERTIES: Comma separated list of properties to be added to the topic message
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("TopicWriter")]
        public static async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            if (topicClient == null)
            {
                var serviceBusConnectionString = System.Environment.GetEnvironmentVariable(SERVICE_BUS_PROPERTY_NAME);
                if (string.IsNullOrEmpty(serviceBusConnectionString))
                    new BadRequestObjectResult($"Service bus connection string is not configured ({SERVICE_BUS_PROPERTY_NAME})");

                var topicName = System.Environment.GetEnvironmentVariable(SERVICE_BUS_TOPIC_PROPERTY_NAME);
                if (string.IsNullOrEmpty(topicName))
                    new BadRequestObjectResult($"Topic name is not configured ({SERVICE_BUS_TOPIC_PROPERTY_NAME})");

            }

            // Stream analytics will send an json array of elements
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var payloadData = JsonConvert.DeserializeObject(requestBody);

            var data = JsonConvert.DeserializeObject(requestBody) as JArray;

            if (data == null || data.Count == 0)
                new BadRequestObjectResult("Expecting an array of json elements in the request body");

            var messages = new List<Message>();
            foreach (var element in data)
            {
                var message = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(element)));
                foreach (var prop in properties)
                {
                    var propValue = element[prop.SourcePropertyName];
                    if (propValue != null)
                        message.UserProperties[prop.TargetPropertyName] = propValue.ToString();
                }

                messages.Add(message);
            }

            try
            {
                await topicClient.SendAsync(messages);

                log.Info($"Sent {messages.Count} messages to topic {topicClient.TopicName}");
            }
            catch (Exception exception)
            {
                log.Error($"{DateTime.Now} :: Exception: {exception.Message}");
                return new BadRequestObjectResult("Error sending message to topic");

            }

            return new OkResult();
        }
    }
}

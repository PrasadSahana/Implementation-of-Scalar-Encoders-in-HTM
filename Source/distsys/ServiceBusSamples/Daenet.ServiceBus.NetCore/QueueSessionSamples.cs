﻿using Microsoft.Azure.ServiceBus;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Daenet.ServiceBus.NetCore
{
    internal class QueueSessionSamples
    {
        const string m_QueueName = "queuesamples/sendreceive";

        static IQueueClient m_QueueClient;

        static ISessionClient m_SessionClient;

        public static async Task RunAsync(int numberOfMessages)
        {

            m_QueueClient = new QueueClient(Credentials.Current.ConnStr, m_QueueName);
            m_SessionClient = new SessionClient(Credentials.Current.ConnStr, m_QueueName, receiveMode: ReceiveMode.PeekLock);
            await SendMessagesAsync(numberOfMessages, "S1");
            await SendMessagesAsync(numberOfMessages, "S2");
            await SendMessagesAsync(numberOfMessages, "S3");

            RunReceivers();

            Console.ReadKey();

            await m_QueueClient.CloseAsync();
        }

        static async Task SendMessagesAsync(int numberOfMessagesToSend, string sessId = null)
        {
            try
            {
                for (var i = 0; i < numberOfMessagesToSend; i++)
                {
                    // Create a new message to send to the queue.
                    string messageBody = $"Message {i}";
                    var message = new Message(Encoding.UTF8.GetBytes(messageBody));
                    message.UserProperties.Add("USECASE", "Session Sample");
                    message.TimeToLive = TimeSpan.FromMinutes(10);
                    if (sessId != null)
                        message.SessionId = sessId;

                    // Write the body of the message to the console.
                    Console.WriteLine($"Sending message: {messageBody}");

                    // Send the message to the queue.
                    await m_QueueClient.SendAsync(message);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"{DateTime.Now} :: Exception: {exception.Message}");
            }
        }


        /// <summary>
        /// Accept explicit session. If not retry accept.
        /// </summary>
        /// <param name="sessionName"></param>
        /// <returns></returns>
        static async Task RunExpiciteReceiver(string sessionName)
        {
            while (true)
            {
                try
                {
                    var session = await m_SessionClient.AcceptMessageSessionAsync(sessionName);

                    while (true)
                    {
                        var message = await session.ReceiveAsync();
                        if (message != null)
                        {
                            Console.WriteLine($"Received message: SessionId:{message.SessionId}, SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{Encoding.UTF8.GetString(message.Body)}");
                            await session.CompleteAsync(message.SystemProperties.LockToken);
                        }
                        else
                        {
                            await session.CloseAsync();
                            // TODO. We do need here try catch if no session found.
                            session = await m_SessionClient.AcceptMessageSessionAsync();
                            Console.WriteLine("no messages..");
                        }
                    }
                }
                catch (AggregateException ex)
                {
                    if (ex.InnerException != null && ex.InnerException is SessionLockLostException)
                    {
                        Console.WriteLine("Session cannot be obtained, because there is an active receiver.");
                        Task.Delay(10000).Wait();
                    }
                    else
                        throw;
                }
            }
        }

        static void RunReceivers()
        {
            List<Task> tasks = new List<Task>();

            var session1 = m_SessionClient.AcceptMessageSessionAsync("S1").Result;
            var session2 = m_SessionClient.AcceptMessageSessionAsync("Ssss2").Result;
            var m =  session1.ReceiveAsync().Result;
                m = session2.ReceiveAsync().Result;

            for (int i = 0; i < 3; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    // Register the function that processes messages.
                    var session = await m_SessionClient.AcceptMessageSessionAsync();

                    while (true)
                    {
                        var message = await session.ReceiveAsync();
                        if (message != null)
                        {
                            Console.WriteLine($"Received message: SessionId:{message.SessionId}, SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{Encoding.UTF8.GetString(message.Body)}");
                            await session.CompleteAsync(message.SystemProperties.LockToken);
                        }
                        else
                        {
                            await session.CloseAsync();
                            // TODO. We do need here try catch if no session found.
                            session = await m_SessionClient.AcceptMessageSessionAsync();
                            Console.WriteLine("no messages..");
                        }
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());
        }


        // Use this handler to examine the exceptions received on the message pump.
        static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            Console.WriteLine($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            Console.WriteLine("Exception context for troubleshooting:");
            Console.WriteLine($"- Endpoint: {context.Endpoint}");
            Console.WriteLine($"- Entity Path: {context.EntityPath}");
            Console.WriteLine($"- Executing Action: {context.Action}");
            return Task.CompletedTask;
        }

    }
}

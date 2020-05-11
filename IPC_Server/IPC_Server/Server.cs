/*
* FILE : Server.cs
* PROJECT : PROG2120 - Windows and Mobile Programming - Assignment #4
* PROGRAMMERS : JMarkic and lpcSetProg
* FIRST VERSION : 2016-11-15
* DESCRIPTION : The files in this program, handle client requests, and navigate
* messages between clients in the chat server program. 
* NOTE: Microsoft Message Queuing (MSMQ) technology  must be enabled in Windows to run. 
* ALSO: Requires client running in queue to function properly. 
* 
*/


using System;
using System.Collections.Generic;
using System.Messaging;
using System.Threading;


namespace IPC_Server
{


    /*
   * Name: Server
   * Purpose: The server class handles clients requests, and navigates messages
   *          between clients int he chat server program. It implements a private
   *          messageQueue for clients to make requests. It spawns a thread to handle
   *          these requests. It additionally uses a class to temp store request
   *          data, and keeps track of clients who've connected and disconnected from
   *          the server
   */
    public class Server
    {
        //attributes
        List<string> clientMasterList = new List<string>();
        string queueName = @".\private$\messageQueue";
        MessageQueue messageQueue;
        Packet requestBuffer = new Packet();


        /*
       * METHOD : startMsgQueue()
       *
       * DESCRIPTION : this method creates and connects to a private queue
       *               on start of the server, it purges the queue of old requests.
       * PARAMETERS : N/A 
       *
       * RETURNS    : N/A 
       */
        public void startMsgQueue()
        {
            try
            {
                if (!MessageQueue.Exists(queueName))
                {
                    messageQueue = MessageQueue.Create(queueName);
                    messageQueue.Purge();
                }
                else
                {
                    messageQueue = new MessageQueue(queueName);
                    messageQueue.Purge();
                }
            }
            catch (MessageQueueException mqex)
            {
                Console.WriteLine("MQ Exception: " + mqex.Message);
            }
        }


        /*
       * METHOD : UpdateClientList()
       *
       * DESCRIPTION :  this method updates the on server list of connected
       *                clients. it calls methods that add/remove clients
       *                that send connect and disconnect requests
       * 
       * PARAMETERS : string : alias
       *              string : machineName
       *              string : messageType
       *
       * RETURNS :  : N/A
       */
        public void UpdateClientList(string alias, string machineName, string messageType)
        {
            //concat alias and machineName
            string clientID_Buffer = alias + "," + machineName;
            //if client request is to connect from server
            if (messageType.Equals("C"))
            {
                //check if client isn't already connected to server
                if (!(clientMasterList.Contains(clientID_Buffer)))
                {
                    //if not add them to the client list on server
                    clientMasterList.Add(clientID_Buffer);
                    //now that client list has been updated, send requests to clients to update
                    // their lists
                    UpdateFriendsListBox(messageType, clientID_Buffer);
                }
                else
                {
                    // A GOOD PLACE TO HAVE A LOG
                   // Console.WriteLine("Can't add to master list, prompt client who requested this.");
                }
            }
            //else if client request is to disconnect from server
            else if (messageType.Equals("D"))
            {
                // if client is connected to the server
                if ((clientMasterList.Contains(clientID_Buffer)))
                {
                    //send requests to clients to update there lists
                    UpdateFriendsListBox(messageType, clientID_Buffer);
                    //remove them from the master list
                    clientMasterList.Remove(clientID_Buffer);
                }
                else
                {
                    //A GOOD PLACE TO HAVE A LOG
                    //Console.WriteLine("Doesn't exist in master list. prompt client who requested this.");
                }    
            }
        }


        /*
       * METHOD : retrieveAliasName()
       *
       * DESCRIPTION : retrieve a client list record and parse out the alias
       * 
       * PARAMETERS : string : clientListRecord   
       * 
       * RETURNS    : string : alias 
       */
        public string retrieveAliasName(string clientListRecord)
        {
            string[] clientInfo = clientListRecord.Split(',');
            string alias = clientInfo[0];

            return alias;
        }


        /*
       * METHOD : retrieveMachineName()
       *
       * DESCRIPTION : retrieve a client list record and parse out the machine name
       * 
       * 
       * PARAMETERS : string : clientListRecord 
       * 
       * RETURNS    : string : machineName
       */
        public string retrieveMachineName(string clientListRecord)
        {
            string[] clientInfo = clientListRecord.Split(',');
            string machineName = clientInfo[1];

            return machineName;
        }


        /*
        * METHOD : WriteToQueue()
        *
        * DESCRIPTION : Send a message to particular client queue
        * 
        * 
        * PARAMETERS : MessageQueue : clientQueue
        *            : string       : data         
        * 
        * RETURNS : N/A
        */
        public void WriteToQueue(MessageQueue clientQueue, string data)
        {
            clientQueue.Send(data);
            clientQueue.Close();
        }


        /*
        * METHOD : constructLisboxUpdate()
        *
        * DESCRIPTION : construct the message that will goes to a client queue
        *               that notifies to update they're local lists
        * 
        * 
        * PARAMETERS : string : clientRecord 
        *              string : messageType
        * 
        * RETURNS    : string : packet : 
        */
        public string constructLisboxUpdate(string clientRecord, string messageType)
        {
            //local variables
            string packet = "";
            string alias = "";
            string update = "";
            string serverID = "";

            //if messages is notifying connect construct that message
            if (messageType.Equals("C"))
            {
                alias = retrieveAliasName(clientRecord);
                update = "C";
                serverID = Environment.MachineName;
                packet = alias + "," + update + "," + serverID + "," + "null" + ",";

            }
            //else if message is notifying disconnect, construct this message
            else if (messageType.Equals("D"))
            {

                alias = retrieveAliasName(clientRecord);
                update = "D";
                serverID = Environment.MachineName;
                packet = alias + "," + update + "," + serverID + "," + "null" + ",";

            }
            //return message
            return packet;
        }


        /*
        * METHOD : UpdateFriendsListBox()
        *
        * DESCRIPTION : constructs and send a connect or disconnect message to a client queue
        *               that will ultimately update a client's local list
        * 
        * 
        * PARAMETERS : string : messageType  
        *              string : originClient
        * 
        * RETURNS    : N/A
        */
        public void UpdateFriendsListBox(string messageType, string originClient)
        {
            //if request is connect
            if (messageType.Equals("C"))
            {
                //select a first record
                for (int i = 0; i < clientMasterList.Count; i++)
                {
                    string machineToSend = retrieveMachineName(clientMasterList[i]);

                    //select a second record
                    for (int j = 0; j < clientMasterList.Count; j++)
                    {
                        //if the first selected record doesn't equal the second
                        if (!(clientMasterList[j].Equals(clientMasterList[i])))
                        {
                            //construct and send an connect request to first client with second clients information
                            string machineName = retrieveMachineName(clientMasterList[j]);
                            MessageQueue clientQueue = new MessageQueue("FormatName:Direct=OS:" + machineToSend + "\\private$\\messageQueue");
                            string packet = constructLisboxUpdate(clientMasterList[j], messageType);
                            WriteToQueue(clientQueue, packet);
                        }
                    }
                }
            }
            else if (messageType.Equals("D"))
            {
                //go through entire client list on server
                for (int j = 0; j < clientMasterList.Count; j++)
                {
                    //if the origin client sending a disconnect request doesn't enter the current client
                    if (!(originClient.Equals(clientMasterList[j])))
                    {
                        //construct and send an disconnect request to that client to update there local list
                        string machineName = retrieveMachineName(clientMasterList[j]);
                        MessageQueue clientQueue = new MessageQueue("FormatName:Direct=OS:" + machineName + "\\private$\\messageQueue");
                        string packet = constructLisboxUpdate(originClient, messageType);
                        WriteToQueue(clientQueue, packet);
                    }
                }
            } 
        }


        /*
        * METHOD : constructPrivateMessage()
        *
        * DESCRIPTION : construct a private message to be sent to a client.
        * 
        * PARAMETERS : Packet : clientRequest 
        * 
        * RETURNS    : string : packet : 
        */
        public string constructPrivateMessage(object request)
        {
            Packet clientRequest = (Packet)request;
            //append information stored temp in a packet to string, in prep to send to a queue
            string packet;
            packet = clientRequest.Alias + "," + clientRequest.MessageType + "," + Environment.MachineName + "," + "null" + "," + clientRequest.Message;
            return packet;
        }


        /*
        * METHOD : SendPrivateMessage()
        *
        * DESCRIPTION : sends a private message to a particular client queue
        * 
        * PARAMETERS : Packet : clientRequest : 
        * 
        * RETURNS    : N/A
        */
        public void SendPrivateMessage(object request)
        {
            Packet clientRequest = (Packet)request;
            string machineID;
            //compare clientRequest data with the records in the client server list
            for(int i = 0; i < clientMasterList.Count; i++)
            {
                string clientAliasRecord = retrieveAliasName(clientMasterList[i]);
                //if they are equal, we'll send a private message to that client
                if (clientAliasRecord.Equals(clientRequest.RequestToChat))
                {
                    machineID = retrieveMachineName(clientMasterList[i]);
                    MessageQueue clientQueue = new MessageQueue("FormatName:Direct=OS:" + machineID + "\\private$\\messageQueue");
                    string packet = constructPrivateMessage(clientRequest);
                    WriteToQueue(clientQueue, packet);
                    break;
                }
            }
        }


        /*
        * METHOD : SendPrivateMessage()
        *
        * DESCRIPTION : sends a group message to a all requested client queue
        * 
        * 
        * PARAMETERS : Packet : clientRequest : 
        * 
        * RETURNS    : N/A
        */
        public void SendGroupMessage(object request)
        {
            Packet clientRequest = (Packet)request;
            //check who's been requested to chat with
            string[] SendToWho = clientRequest.RequestToChat.Split('|');
         
            if (clientRequest.MessageType.Equals("G"))
            {
                //prepare to check each request client to chat with exists
                for (int i = 0; i < SendToWho.Length; i++)
                {
                    string machineToSend = SendToWho[i];

                    //confirm they exists
                    for (int j = 0; j < clientMasterList.Count; j++)
                    {
                        //if they exist send a message to each client requested
                        if (machineToSend.Equals(retrieveAliasName(clientMasterList[j])))
                        {
                            string machineName = retrieveMachineName(clientMasterList[j]);
                            MessageQueue clientQueue = new MessageQueue("FormatName:Direct=OS:" + machineName + "\\private$\\messageQueue");
                            string packet = constructPrivateMessage(clientRequest);
                            WriteToQueue(clientQueue, packet);
                        }
                    }
                }
            }
        }


        /*
        * METHOD : SendPrivateMessage()
        *
        * DESCRIPTION : sends a message to all existing clients in the client
        *               server list
        * 
        * 
        * PARAMETERS : Packet : clientRequest 
        * 
        * RETURNS    : N/A
        */
        public void SendBroadcastMessage(object request)
        {
            Packet clientRequest = (Packet)request;
            //retrieve requesting client
            string originMachine = clientRequest.Alias;

            if (clientRequest.MessageType.Equals("B"))
            {
                for (int j = 0; j < clientMasterList.Count; j++)
                {
                    //if requesting clients does not equal current record send a message to that record client
                    if (!(originMachine.Equals(retrieveAliasName(clientMasterList[j]))))
                    {
                        string machineName = retrieveMachineName(clientMasterList[j]);
                        MessageQueue clientQueue = new MessageQueue("FormatName:Direct=OS:" + machineName + "\\private$\\messageQueue");
                        string packet = constructPrivateMessage(clientRequest);
                        WriteToQueue(clientQueue, packet);
                    }
                }
            }
        }


        /*
        * METHOD : DeconstructMessage()
        *
        * DESCRIPTION : this deconstructs a message read from the server queue
        *               and packages it into an object to preserve values, and ease
        *               of access
        * 
        * PARAMETERS : string : packet : 
        * 
        * RETURNS    : N/A
        */
        public void DeconstructMessage(string packet)
        {
            string[] messageComposition = packet.Split(',');

            //Packet clientRequest = new Packet();

            requestBuffer.Alias = messageComposition[0];
            requestBuffer.MessageType = messageComposition[1];
            requestBuffer.ClientMachine = messageComposition[2];
            requestBuffer.Message = messageComposition[3];
            requestBuffer.RequestToChat = messageComposition[4];
        }


        /*
        * METHOD : ProcessRequest()
        *
        * DESCRIPTION : this method determines what type of message has been
        *               read from the queue. Possible messages are connect, disconnect,
        *               private chat, group chat, and broadcast chat.
        * 
        * 
        * PARAMETERS : object : request : 
        * 
        * RETURNS    : N/A
        */
        public void ProcessRequest(object request)
        {
            //cast object parameter to a usable packet object
            Packet clientRequest = (Packet)request;
            //if request is to connect or disconnect
            if (clientRequest.MessageType.Equals("C") || clientRequest.MessageType.Equals("D"))
            {
                //update client server list
                UpdateClientList(clientRequest.Alias, clientRequest.ClientMachine, clientRequest.MessageType);
            }
            //if private
            else if (clientRequest.MessageType.Equals("P"))
            {
                //send private message to appropriate client
                SendPrivateMessage(clientRequest);
            }
            //if broadcast
            else if (clientRequest.MessageType.Equals("B"))
            {
                //send broadcast message to all connected clients
                SendBroadcastMessage(clientRequest);
            }
            // if group
            else if (clientRequest.MessageType.Equals("G"))
            {
                //send group message to all requested clients
                SendGroupMessage(clientRequest);
            }   
        }


        /*
        * METHOD : readMessages()
        *
        * DESCRIPTION : reads messages from the queue, if no message are written to queue
        *               it sit at a blocking call, when there's a request to process, create
        *               a thread to handle that, to free up reading more requests from the queue
        * 
        * PARAMETERS : N/A
        * 
        * RETURNS    : N/A
        */
        public void readMessages()
        {
            bool finished = false;
            //XML formatter
            messageQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(string) });

            //while not finished reading messages
            while (!finished)
            {
                try
                {
                    string packet;
                    //unpack XML to get the client request in workable format
                    packet = (string)messageQueue.Receive().Body;

                    //if request isn't NULL
                    if (packet != null)
                    {
                        //deconstruct the message and thread a process to process the message request
                        DeconstructMessage(packet);
                        Packet clientRequest = requestBuffer;
                        Thread processRequests = new Thread(ProcessRequest);
                        // Start the thread
                        processRequests.Start(clientRequest);
                        //ProcessRequest(clientRequest);

                        if (packet == "Shutdown")
                        {
                            finished = true;
                        }
                    }
                }
                catch (MessageQueueException mqex)
                {
                    Console.WriteLine("MQ Exception: " + mqex.Message);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception:" + ex.Message);
                }

                messageQueue.Close();
            }
        }


        //Main 
        static void Main(string[] args)
        {
            string serverName = Environment.MachineName;
            Server startServer = new Server();
            startServer.startMsgQueue();
            startServer.readMessages(); 
        }

    }
}

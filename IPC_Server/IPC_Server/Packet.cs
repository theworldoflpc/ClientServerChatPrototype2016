/*
* FILE : Packet.cs
* PROJECT : PROG2120 - Windows and Mobile Programming - Assignment #4
* PROGRAMMERS : JMarkic and lpcSetProg
* FIRST VERSION : 2016-11-15
* DESCRIPTION : This file contains the properties for the Assignment 4 
* server program. 
*/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IPC_Server
{
    /*
   * Name: Packet
   * Purpose: define properties for the packet object.
   */
    class Packet
    {
        private string alias;
        private string messageType;
        private string clientMachine;
        private string requestToChat;
        private string message;


        public Packet()
        {
        }

        public string Alias
        {
            get { return alias; }
            set { alias = value; }
        }

        public string MessageType
        {
            get { return messageType; }
            set { messageType = value; }
        }
        public string ClientMachine
        {
            get { return clientMachine; }
            set { clientMachine = value; }
        }

        public string RequestToChat
        {
            get { return requestToChat; }
            set { requestToChat = value; }
        }

        public string Message
        {
            get { return message; }
            set { message = value; }
        }
    }
}

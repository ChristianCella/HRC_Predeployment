/*
Test1: more than one array is sent to C# (in sequence)
*/

using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Tecnomatix.Engineering;
using System.Collections.Generic;
using Tecnomatix.Engineering.Olp;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static public void Main(ref StringWriter output)
    {
        TcpListener server = null;
        try
        {
            // Define the number of simulations
            int Nsim = 5;
            int port = 12345;

            // Start listening for incoming connections
            server = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            server.Start();
            output.Write("Server started...");

            // Accept a client connection
            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();

            // Loop for all the simulations
            for (int ii = 0; ii < Nsim - 1; ii++)
            {
                // Receive sequence array
                var sequence = ReceiveNumpyArray(stream);
                output.Write("Sequence:");
                PrintArray(sequence, output);

                // Receive shared array
                var shared = ReceiveNumpyArray(stream);
                output.Write("Shared:");
                PrintArray(shared, output);

                // Receive starting_times array
                var starting_times = ReceiveNumpyArray(stream);
                output.Write("Starting Times:");
                PrintArray(starting_times, output);

                // Separate the display information on the terminal
                output.Write("\n");

                // Send the trigger_end back to Python
                string trigger_end = ii.ToString();
                byte[] byte_trigger_end = Encoding.ASCII.GetBytes(trigger_end);
                stream.Write(byte_trigger_end, 0, byte_trigger_end.Length);
            }

            // Close all the instances
            stream.Close();
            client.Close();
            server.Stop();
        }
        catch (Exception e)
        {
            output.Write("Exception: " + e.Message);
        }
    }

    // Definition of custom functions   
    static int[,] ReceiveNumpyArray(NetworkStream stream)
    {
        // Receive the shape of the array
        byte[] shapeBuffer = new byte[8]; // Assuming the shape is of two int32 values
        stream.Read(shapeBuffer, 0, shapeBuffer.Length);
        int rows = BitConverter.ToInt32(shapeBuffer, 0);
        int cols = BitConverter.ToInt32(shapeBuffer, 4);

        // Receive the array data
        int arraySize = rows * cols * sizeof(int); // Assuming int32 values
        byte[] arrayBuffer = new byte[arraySize];
        stream.Read(arrayBuffer, 0, arrayBuffer.Length);

        // Convert byte array to int array
        int[,] array = new int[rows, cols];
        Buffer.BlockCopy(arrayBuffer, 0, array, 0, arrayBuffer.Length);

        return array;
    }

    static void PrintArray(int[,] array, StringWriter m_output)
    {
        int rows = array.GetLength(0);
        int cols = array.GetLength(1);
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                m_output.Write(array[i, j] + " ");
            }
            m_output.Write("\n");
        }
    }
}

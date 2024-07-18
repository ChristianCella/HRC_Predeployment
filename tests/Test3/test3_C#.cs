/* 
Test3: more than one array (both integers and strings) is received from python
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
using System.Linq;

class Program
{
    static public void Main(ref StringWriter output)
    {
        TcpListener server = null;
        try
        {
            int Nsim = 5;
            int port = 12345;
            server = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            server.Start();
            output.Write("Server started...");

            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();

            for (int ii = 0; ii < Nsim - 1; ii++)
            {
                var sequence = ReceiveNumpyArray(stream);
                output.Write("Sequence:");
                PrintArray(sequence, output);

                var shared = ReceiveNumpyArray(stream);
                output.Write("Shared:");
                PrintArray(shared, output);

                var starting_times = ReceiveNumpyArray(stream);
                output.Write("Starting Times:");
                PrintArray(starting_times, output);

                var stringData = ReceiveStringList(stream);
                output.Write("String Data:");
                foreach (var str in stringData)
                {
                    output.Write(str + " ");
                }
                output.Write("\n");

                string trigger_end = ii.ToString();
                byte[] byte_trigger_end = Encoding.ASCII.GetBytes(trigger_end);
                stream.Write(byte_trigger_end, 0, byte_trigger_end.Length);
            }

            stream.Close();
            client.Close();
            server.Stop();
        }
        catch (Exception e)
        {
            output.Write("Exception: " + e.Message);
        }
    }

    static int[,] ReceiveNumpyArray(NetworkStream stream)
    {
        byte[] shapeBuffer = new byte[8];
        stream.Read(shapeBuffer, 0, shapeBuffer.Length);
        int rows = BitConverter.ToInt32(shapeBuffer, 0);
        int cols = BitConverter.ToInt32(shapeBuffer, 4);

        int arraySize = rows * cols * sizeof(int);
        byte[] arrayBuffer = new byte[arraySize];
        stream.Read(arrayBuffer, 0, arrayBuffer.Length);

        int[,] array = new int[rows, cols];
        Buffer.BlockCopy(arrayBuffer, 0, array, 0, arrayBuffer.Length);

        return array;
    }

    static List<string> ReceiveStringList(NetworkStream stream)
    {
        byte[] lengthBuffer = new byte[4];
        stream.Read(lengthBuffer, 0, lengthBuffer.Length);
        int length = BitConverter.ToInt32(lengthBuffer, 0);

        byte[] stringBuffer = new byte[length];
        stream.Read(stringBuffer, 0, stringBuffer.Length);

        string data = Encoding.UTF8.GetString(stringBuffer);
        List<string> stringList = data.Split(',').ToList();

        return stringList;
    }

    static void PrintArray(int[,] array, StringWriter output)
    {
        int rows = array.GetLength(0);
        int cols = array.GetLength(1);
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                output.Write(array[i, j] + " ");
            }
            output.Write("\n");
        }
    }
}

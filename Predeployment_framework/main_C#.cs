/*
This is the first attempt for a structured communication.
* In Python, the sequence of operations is defined ("Who does what"): 0 = robot, 1 = human.
* In C# this array is received and the operations are created (type "Pick&Place" both for human and robot).
* Each Operation is run singularly, the time is calculated and the results are sent back to Python.
* Python mimics the "Scheduling" algorithm (not yet implemented), finds the optimal schedule and sends the times back to C#.
* In C# the times are received and a function concatenates the operations (Gantt chart).
* The complete collaborative operation is run and a boolean flag is sent to Python to notify possible collisions.
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
        	// Common name between objects
        	string obj_name = "YAOSC_cube";
        	
            // Define the number of simulations
            int port = 12345;

            // Start listening for incoming connections
            server = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            server.Start();
            output.Write("Server started...");

            // Accept a client connection
            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();

            // Receive the shared data
            var shared_data = ReceiveNumpyArray(stream);
            output.Write("The shared data are:");
            PrintArray(shared_data, output);

			// Acquire the number of simulations
            int Nsim = shared_data[0, 0];
            int Ndecimals = shared_data[0, 1];
            int multiplier = shared_data[0, 2];
            int Ntasks = shared_data[0, 3];
            int Nitems = shared_data[0, 4];
            int Ncoordinates = shared_data[0, 5];
            
            // Get the positions of the objects

            // Loop for all the simulations
            for (int ii = 1; ii < Nsim; ii++)
            {
            
            	// Receive the layout (remember: divide it by 'multiplier')
                var layout = ReceiveNumpyArray(stream);
                output.Write("The layout is:");
                PrintArray(layout, output);
                
                // Refresh the positions of the objects
                
                for (int jj = 1; jj < Nitems + 1; jj ++)
                {
                	RefreshResources(obj_name, jj, layout, Ncoordinates, multiplier, output);
                	TxApplication.RefreshDisplay();
                	output.Write(obj_name + jj);
                }
            	
                // Receive sequence array
                var sequence = ReceiveNumpyArray(stream);
                output.Write("Sequence:");
                PrintArray(sequence, output);              

                // Send the array of times back to python
                int[] kpis = { 1 + ii, 2 + ii, 3 + ii, 4 + ii };
                string data = string.Join(",", kpis);
                byte[] kpi_vec = Encoding.ASCII.GetBytes(data); // ASCII encoding              
                stream.Write(kpi_vec, 0, kpi_vec.Length); // Write on the stream

                // Receive schedulding array array
                var scheduling = ReceiveNumpyArray(stream);
                output.Write("The scheduling is:");
                PrintArray(scheduling, output);

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
    
    // Function to refresh the position of the objects
    static void RefreshResources(string name, int idx, int[,] positions, int Ncoordinates, int multiplier, StringWriter m_output)
    {
    	// Get the correct object
    	TxObjectList obj_pick = TxApplication.ActiveSelection.GetItems();
		obj_pick = TxApplication.ActiveDocument.GetObjectsByName(name + idx);
		m_output.Write(name + idx + "\n");
		var obj = obj_pick[0] as ITxLocatableObject; // take the first object with taht name
		
		// Extract the relevant portion of the positions array
    	int startIdx = Ncoordinates * (idx - 1);
    	int endIdx = Ncoordinates * idx;
	
    	int[,] reduced_positions = new int[1, Ncoordinates];
    	for (int i = 0; i < Ncoordinates; i++)
    	{
        	reduced_positions[0, i] = positions[0, startIdx + i];
    	}
		
		// Scale back the positions and rotations
		var posx = reduced_positions[0, 0] / multiplier;
		var posy = reduced_positions[0, 1] / multiplier;
		var posz = reduced_positions[0, 2] / multiplier;
		var rx = reduced_positions[0, 3] / multiplier;
		var ry = reduced_positions[0, 4] / multiplier;
		var rz = reduced_positions[0, 5] / multiplier;
		//m_output.Write(posx.ToString() + "\n"); // Check
			
		TxTransformation rotRob = new TxTransformation(new TxVector(rx, ry, rz), 
		TxTransformation.TxRotationType.RPY_XYZ);
		obj.AbsoluteLocation = rotRob;
	
		var pos = new TxTransformation(obj.LocationRelativeToWorkingFrame);
		pos.Translation = new TxVector(posx, posy, posz);
		obj.LocationRelativeToWorkingFrame = pos;
		
		// Refresh the display							
		TxApplication.RefreshDisplay();
    }
}

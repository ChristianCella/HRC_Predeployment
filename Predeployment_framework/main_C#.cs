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
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.ComponentModel;
using Tecnomatix.Engineering;
using EngineeringInternalExtension;
using System.Collections.Generic;
using Tecnomatix.Engineering.Olp;
using System.Collections.Generic;
using System.Linq;
using Tecnomatix.Engineering.Plc;
using Tecnomatix.Engineering.Utilities;
using Tecnomatix.Engineering.ModelObjects;
using Jack.Toolkit;
using Jack.Toolkit.TSB;
using scaleParam = Jack.Toolkit.jcAdvancedAnthroScale.input;

class Program
{
	
	// Useful variables
    static bool verbose = false;
    
    static public void Main(ref StringWriter output)
    {
        TcpListener server = null;
        try
        {
        	// Common names of objects and operations
        	string obj_name = "YAOSC_cube";
        	string op_name = "HumanPickAndPlace";
        	string target_name = "NewTray";
        	string fr_cube = "fr_cube";
        	
            // Define the number of simulations
            int port = 12345;

            // Start listening for incoming connections
            server = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            server.Start();
            output.Write("Connection happened successfully!\n");

            // Accept a client connection
            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();

            // Receive the shared data
            var shared_data = ReceiveNumpyArray(stream);
            if (verbose)
            {
                output.Write("The shared data are: ");
                PrintArray(shared_data, output);
            }

			// Acquire all the data that will not be changed anymore
            int Nsim = shared_data[0, 0];
            int Ndecimals = shared_data[0, 1];
            int multiplier = shared_data[0, 2];
            int Ntasks = shared_data[0, 3];
            int Nitems = shared_data[0, 4];
            int Ncoordinates = shared_data[0, 5];

            // Loop for all the simulations
            for (int ii = 1; ii < Nsim; ii++)
            {
            
            	// Receive the layout (remember: divide it by 'multiplier' to obtain the real values)
                var layout = ReceiveNumpyArray(stream);
                if (verbose)
                {
                    output.Write("The received layout is: ");
                    PrintArray(layout, output);
                }
                
                // Refresh the positions of the objects               
                for (int jj = 1; jj < Nitems + 1; jj ++)
                {
                	RefreshResources(obj_name, jj, layout, Ncoordinates, multiplier, output, verbose);
                	TxApplication.RefreshDisplay();
                }
            	
                // Receive sequence array
                var sequence = ReceiveNumpyArray(stream);
                if (verbose)
                {
                	output.Write("The sequence is: ");
                	PrintArray(sequence, output); 
                }
                
                // Create the operations for the human (index = 1)
                for (int kk = 0; kk < Ntasks; kk ++)
                {
                	if (sequence[0, kk] == 1)
                	{
                		output.Write("MIAO");
                		HumanPickPlace(op_name, obj_name, target_name, kk + 1, fr_cube, output);
                	}
                }
                             
                // Send the array of times back to python
                int[] kpis = { 1 + ii, 2 + ii, 3 + ii, 4 + ii };
                string data = string.Join(",", kpis);
                byte[] kpi_vec = Encoding.ASCII.GetBytes(data); // ASCII encoding              
                stream.Write(kpi_vec, 0, kpi_vec.Length); // Write on the stream

                // Receive schedulding array array
                var scheduling = ReceiveNumpyArray(stream);
                if (verbose)
                {
                	output.Write("The scheduling is: ");
                	PrintArray(scheduling, output);
                }
              
                // Send the trigger_end back to Python
                string trigger_end = ii.ToString();
                byte[] byte_trigger_end = Encoding.ASCII.GetBytes(trigger_end);
                stream.Write(byte_trigger_end, 0, byte_trigger_end.Length);
            }

            // Close all the instances
            output.Write("Everything worked fine!");
            stream.Close();
            client.Close();
            server.Stop();
        }
        catch (Exception e)
        {
        	output.Write("Something went wrong!");
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
    static void RefreshResources(string name, int idx, int[,] positions, int Ncoordinates, 
                                int multiplier, StringWriter m_output, bool verbose)
    {
    	// Get the correct object
    	TxObjectList obj_pick = TxApplication.ActiveSelection.GetItems();
		obj_pick = TxApplication.ActiveDocument.GetObjectsByName(name + idx);
		var obj = obj_pick[0] as ITxLocatableObject; // take the first object with that name
		
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

        if (verbose)
        {
            m_output.Write("The new x position is: " + posx.ToString() + "\n");
            m_output.Write("The new y position is: " + posy.ToString() + "\n");
            m_output.Write("The new z position is: " + posz.ToString() + "\n");
        }
		
		// Impose the new position and rotation	
		TxTransformation rotRob = new TxTransformation(new TxVector(rx, ry, rz), 
		TxTransformation.TxRotationType.RPY_XYZ);
		obj.AbsoluteLocation = rotRob;
	
		var pos = new TxTransformation(obj.LocationRelativeToWorkingFrame);
		pos.Translation = new TxVector(posx, posy, posz);
		obj.LocationRelativeToWorkingFrame = pos;
		
		// Refresh the display							
		TxApplication.RefreshDisplay();
    }
    
    static void HumanPickPlace(string op_name, string obj_name, string target_name, int op_idx, string fr_cube, StringWriter m_output)
    {
    	// Initialization variables for the pick and place 	
    	TxHumanTsbSimulationOperation op = null; 
    	TxHumanTSBTaskCreationDataEx taskCreationData = new TxHumanTSBTaskCreationDataEx();
    	
    	// Get the human		
		TxObjectList humans = TxApplication.ActiveSelection.GetItems();
		humans = TxApplication.ActiveDocument.GetObjectsByName("Jack");
		TxHuman human = humans[0] as TxHuman;
		
		// Apply a certain position to the human and save it in a variable
		human.ApplyPosture("Leaned");
		TxHumanPosture posture_lean = human.GetPosture();
		TxApplication.RefreshDisplay();
		
		human.ApplyPosture("UserHome"); // Re-initialize the human in tne home position
		TxHumanPosture posture_home = human.GetPosture(); 
		TxApplication.RefreshDisplay();
		
		// Get the reference frame of the cube		
		TxObjectList ref_frame_cube = TxApplication.ActiveSelection.GetItems();
		ref_frame_cube = TxApplication.ActiveDocument.GetObjectsByName(fr_cube + op_idx);
		TxFrame frame_cube = ref_frame_cube[0] as TxFrame;
		
		// Get the object for the pick	
		TxObjectList cube_pick = TxApplication.ActiveSelection.GetItems();
		cube_pick = TxApplication.ActiveDocument.GetObjectsByName(obj_name + op_idx);
		var cube1 = cube_pick[0] as ITxLocatableObject;
		
		// Get the position for the place
		TxObjectList target = TxApplication.ActiveSelection.GetItems();
		target = TxApplication.ActiveDocument.GetObjectsByName(target_name);
		var targ = target[0] as ITxLocatableObject;
		
		// Save the object current position
		var position_pick = new TxTransformation(cube1.AbsoluteLocation);
		var posy_pick = position_pick[1, 3];
		
		// Decide which hand should grasp the cube as a function of the position of the cube		
		if (posy_pick >= 0) // grasp with right hand
    	{
    		taskCreationData.Effector = HumanTsbEffector.RIGHT_HAND;
    		TxTransformation rightHandTarget = null;
        	taskCreationData.RightHandAutoGrasp = true;
        	rightHandTarget = new TxTransformation();
        	rightHandTarget = (frame_cube as ITxLocatableObject).AbsoluteLocation;
        	taskCreationData.RightHandAutoGraspTargetLocation =  rightHandTarget *= new TxTransformation(new TxVector(0, 0, 30), TxTransformation.TxTransformationType.Translate);
    	}
    	else // Grasp with left hand
    	{
    		taskCreationData.Effector = HumanTsbEffector.LEFT_HAND;
			TxTransformation leftHandTarget = null;
        	taskCreationData.LeftHandAutoGrasp = true;
        	leftHandTarget = new TxTransformation();
        	leftHandTarget = (frame_cube as ITxLocatableObject).AbsoluteLocation;
        	taskCreationData.LeftHandAutoGraspTargetLocation =  leftHandTarget *= new TxTransformation(new TxVector(0, 0, 30), TxTransformation.TxTransformationType.Translate);
    	}
    	
    	// Create the simulation  		
    	op = TxHumanTSBSimulationUtilsEx.CreateSimulation(op_name + op_idx);
    	op.SetInitialContext();
        op.ForceResimulation();
        
        // Create the 'get' task 		
		taskCreationData.Human = human;						
		taskCreationData.PrimaryObject = cube1;               			
		taskCreationData.TaskType = TsbTaskType.HUMAN_Get;
		taskCreationData.TargetLocation = position_pick;	
		taskCreationData.KeepUninvolvedHandStill = true;				
		TxHumanTsbTaskOperation tsbGetTask = op.CreateTask(taskCreationData);
		op.ApplyTask(tsbGetTask, 1);
   		TxApplication.RefreshDisplay();	
		
		// Set the intermediate pose to be reached by the human
		human.SetPosture(posture_lean);	
		
		// Create the 'pose' task		
		taskCreationData.Human = human;					
   		taskCreationData.TaskType = TsbTaskType.HUMAN_Pose;	
		taskCreationData.TaskDuration = 0.7;		
   		TxHumanTsbTaskOperation tsbPoseTaskInt = op.CreateTask(taskCreationData, tsbGetTask); 
   		op.ApplyTask(tsbPoseTaskInt, 1);
   		TxApplication.RefreshDisplay();
   		
   		// Set the place position (if you need, also rotate the object)	
   		var target_place = new TxTransformation(targ.AbsoluteLocation);	
   		var position_place = new TxTransformation(cube1.AbsoluteLocation);
		position_place.Translation = new TxVector(target_place[0, 3], target_place[1, 3], target_place[2, 3] + 20);
		position_place.RotationRPY_ZYX = new TxVector(0, 0, 0);
				
		// Create the 'put' task			
		taskCreationData.Human = human;
   		taskCreationData.PrimaryObject = cube1;
   		taskCreationData.TargetLocation = position_place;					
   		taskCreationData.TaskType = TsbTaskType.HUMAN_Put;			
   		TxHumanTsbTaskOperation tsbPutTask = op.CreateTask(taskCreationData, tsbPoseTaskInt);
   		op.ApplyTask(tsbPutTask, 1);
   		TxApplication.RefreshDisplay();
   		
   		// Set the correct pose to be reached by the human
		human.SetPosture(posture_home);
		
		// Create the 'pose' task		
		taskCreationData.Human = human;					
   		taskCreationData.TaskType = TsbTaskType.HUMAN_Pose;	
		taskCreationData.TaskDuration = 0.7;		
   		TxHumanTsbTaskOperation tsbPoseTask = op.CreateTask(taskCreationData, tsbPutTask);
   		op.ApplyTask(tsbPoseTask, 1);
   		TxApplication.RefreshDisplay();
    }
}

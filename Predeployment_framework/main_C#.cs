/*
This is the first attempt for a structured communication.
* In Python, the sequence of operations is defined ("Who does what"): 0 = robot, 1 = human.
* In C# this array is received and the operations are created (type "Pick&Place" both for human and robot).
* Each Operation is run singularly, the time is calculated and the results are sent back to Python.
* Python mimics the "Scheduling" algorithm (not yet implemented), finds the optimal schedule and sends the times back to C#.
* In C# the times are received and a function concatenates the operations (Gantt chart).
* The complete collaborative operation is run and a boolean flag is sent to Python to notify possible collisions.

NOTE:
robotic operations ==> 'TxContinuousRoboticOperation'
human operations ==> 'TxHumanTsbSimulationOperation'
*/

using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
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
    static bool verbose = true;
    
    static public void Main(ref StringWriter output)
    {
        TcpListener server = null;
        try
        {       	
            // Define the number of simulations
            int port = 12345;

            // Start listening for incoming connections
            server = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            server.Start();
            output.Write("Connection happened successfully!\n");

            // Accept a client connection
            TcpClient client = server.AcceptTcpClient();
            NetworkStream stream = client.GetStream();

            // Receive the shared data (integers)
            var shared_data = ReceiveNumpyArray(stream);
            if (verbose)
            {
                output.Write("The shared data are: ");
                PrintArray(shared_data, output);
            }

			// Unpack all the integers data that will not be chnages anymore
            int Nsim = shared_data[0, 0];
            int Ndecimals = shared_data[0, 1];
            int multiplier = shared_data[0, 2];
            int Ntasks = shared_data[0, 3];
            int Nitems = shared_data[0, 4];
            int Ncoordinates = shared_data[0, 5];

			// Receive the shared 'general purpose' data (strings)
			var stringData = ReceiveStringList(stream);
			if (verbose)
			{
				foreach (var str in stringData)
				{
					output.Write(str + " ");
				}
				output.Write("\n");
			}
			
			// Unpack all the strings that will not be chnaged anymore
			string obj_name = stringData[0];
			string op_name = stringData[1];
			string op_rob_name = stringData[2];
			string target_name = stringData[3];
			string fr_cube = stringData[4];
			string human_name = stringData[5];
			string robot_name = stringData[6];
			string waypoint_name = stringData[7];
                
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
                
                // Refresh the positions of the objects ==> Call the function defined below              
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
                
                // Create the operations and get their duration
				List<int> kpis = new List<int> ();
				double time_op;
				int time_multiplied;

                for (int kk = 0; kk < Ntasks; kk ++)
                {
                	if (sequence[0, kk] == 1)
                	{
                		HumanPickPlace(human_name, op_name, obj_name, target_name, kk + 1, fr_cube, output);

						// Get the created operation
						TxHumanTsbSimulationOperation op = TxApplication.ActiveDocument.GetObjectsByName(op_name + (kk + 1).ToString())[0] as TxHumanTsbSimulationOperation;

						// calculate the time
						time_op = Math.Round(op.Duration, Ndecimals) * multiplier;
						time_multiplied = (int)time_op;
						output.Write("The time is: " + op.Duration + "\n");

						// Augment the array
						kpis.Add(time_multiplied);
                	}
                	else if (sequence[0, kk] == 0)
                	{
                		RobotPickPlace(robot_name, obj_name, target_name, op_rob_name, kk + 1, output, verbose);

						// Get the created operation (it must be executed to get the time, unlike for the human)
						var op = TxApplication.ActiveDocument.OperationRoot.GetAllDescendants(new 
						TxTypeFilter(typeof(TxContinuousRoboticOperation))).FirstOrDefault(x => x.Name.Equals(op_rob_name + (kk + 1).ToString())) as 
						TxContinuousRoboticOperation;     
						TxApplication.ActiveDocument.CurrentOperation = op;

						// Play the operation silently, then refresh the display and rewind the simulation   
						TxSimulationPlayer simPlayer = TxApplication.ActiveDocument.SimulationPlayer;					
						simPlayer.PlaySilently();
						simPlayer.Rewind();

						// calculate the time
						time_op = Math.Round(op.Duration, Ndecimals) * multiplier;
						time_multiplied = (int)time_op;
						output.Write("The time is: " + op.Duration + "\n");

						// Augment the array
						kpis.Add(time_multiplied);
                	}
                }
                             
                // Send the array of times back to python
                string data = string.Join(",", kpis);
                byte[] kpi_vec = Encoding.ASCII.GetBytes(data); // ASCII encoding              
                stream.Write(kpi_vec, 0, kpi_vec.Length); // Write on the stream

				// Receive sequence of operations
                var assembly_sequence = ReceiveNumpyArray(stream);
                if (verbose)
                {
                	output.Write("The assembly sequence is: ");
                	PrintArray(assembly_sequence, output);
                }

                // Receive schedulding array
                var scheduling = ReceiveNumpyArray(stream);
                if (verbose)
                {
                	output.Write("The scheduling is: ");
                	PrintArray(scheduling, output);
                }

				// Get all the created operations
				TxObjectList operations = GetCreatedOperations(verbose, output);

				// Create the Gantt Chart
				CreateGantt(operations, assembly_sequence, scheduling, multiplier, output, verbose);
              
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
        	output.Write("There's an exception: something went wrong!");
            output.Write("Exception: " + e.Message);
        }
    }

    // --------------------------------------- Custom functions --------------------------------------- //
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
		TxTransformation rot_obj = new TxTransformation(new TxVector(rx, ry, rz), 
		TxTransformation.TxRotationType.RPY_XYZ);
		obj.AbsoluteLocation = rot_obj;
	
		var pos = new TxTransformation(obj.LocationRelativeToWorkingFrame);
		pos.Translation = new TxVector(posx, posy, posz);
		obj.LocationRelativeToWorkingFrame = pos;
		
		// Refresh the display							
		TxApplication.RefreshDisplay();
    }
    
    static void HumanPickPlace(string human_name, string op_name, string obj_name, string target_name, int op_idx, string fr_cube, StringWriter m_output)
    {
    	// Initialization variables for the pick and place 	
    	TxHumanTsbSimulationOperation op = null; 
    	TxHumanTSBTaskCreationDataEx taskCreationData = new TxHumanTSBTaskCreationDataEx();
    	
    	// Get the human		
		TxObjectList humans = TxApplication.ActiveSelection.GetItems();
		humans = TxApplication.ActiveDocument.GetObjectsByName(human_name);
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

	// Function to add OLP commands
	static void AddOpenCloseCommand(string point_name, string gripper_name, string pose_name, string tcp_name)
	{
		// Save the second point to close the gripper		
		TxRoboticViaLocationOperation Waypoint =  TxApplication.ActiveDocument.
  		GetObjectsByName(point_name)[0] as TxRoboticViaLocationOperation;
  		
  		// Save the gripper "Camozzi gripper" 	
  		ITxObject Gripper = TxApplication.ActiveDocument.
		GetObjectsByName(gripper_name)[0] as TxGripper;
  		
  		// Save the pose "Gripper Closed"  		
  		ITxObject Pose = TxApplication.ActiveDocument.
		GetObjectsByName(pose_name)[0] as TxPose;
  		
  		// Save the reference frame of the gripper 		
  		ITxObject tGripper = TxApplication.ActiveDocument.
		GetObjectsByName(tcp_name)[0] as TxFrame;
		
		// Create an array called "elements" and the command to be written in it
    	ArrayList elements1 = new ArrayList();
    	ArrayList elements2 = new ArrayList();
    	ArrayList elements3 = new ArrayList();
    	ArrayList elements4 = new ArrayList();
    	ArrayList elements5 = new ArrayList();
	
    	var myCmd1 = new TxRoboticCompositeCommandStringElement("# Destination");
    	var myCmd11 = new TxRoboticCompositeCommandTxObjectElement(Gripper);

    	var myCmd2 = new TxRoboticCompositeCommandStringElement("# Drive");
    	var myCmd21 = new TxRoboticCompositeCommandTxObjectElement(Pose);

    	var myCmd3 = new TxRoboticCompositeCommandStringElement("# Destination");
    	var myCmd31 = new TxRoboticCompositeCommandTxObjectElement(Gripper);

    	var myCmd4 = new TxRoboticCompositeCommandStringElement("# WaitDevice");
    	var myCmd41 = new TxRoboticCompositeCommandTxObjectElement(Pose);

    	var myCmd5 = new TxRoboticCompositeCommandStringElement("# Grip"); // For the closing
		if (pose_name == "OPEN")
		{
			myCmd5 = new TxRoboticCompositeCommandStringElement("# Release"); // For the closing
		}
    	var myCmd51 = new TxRoboticCompositeCommandTxObjectElement(tGripper);
	
 		// First line of command	
    	elements1.Add(myCmd1);
    	elements1.Add(myCmd11);
	  	
    	TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData1 =
    	new TxRoboticCompositeCommandCreationData(elements1);
	
    	Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData1);
    	
		// Second line of command
    	elements2.Add(myCmd2);
    	elements2.Add(myCmd21);

    	TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData2 =
    	new TxRoboticCompositeCommandCreationData(elements2);
	
    	Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData2);
    	
		// Third line of command
    	elements3.Add(myCmd3);
    	elements3.Add(myCmd31);

    	TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData3 =
    	new TxRoboticCompositeCommandCreationData(elements3);
	
    	Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData3);
    	
		// Fourth line of command
    	elements4.Add(myCmd4);
    	elements4.Add(myCmd41);

    	TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData4 =
    	new TxRoboticCompositeCommandCreationData(elements4);
	
    	Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData4);
    	
		// Fifth line of command	
    	elements5.Add(myCmd5);
    	elements5.Add(myCmd51);

    	TxRoboticCompositeCommandCreationData txRoboticCompositeCommandCreationData5 =
    	new TxRoboticCompositeCommandCreationData(elements5);
	
    	Waypoint.CreateCompositeCommand(txRoboticCompositeCommandCreationData5);
       
	}

	// Function to create robot programs automatically
    static void RobotPickPlace(string robot_name, string pick_target, string place_target, string op_name, 
								int op_idx, StringWriter m_output, bool verbose)
    {
    	
    	string new_tcp = "tf_tcp_1";
    	string new_motion_type = "MoveL";
		string new_speed = "1000";
		string new_accel = "1200";
		string new_blend = "0";
		string new_coord = "Cartesian";

        // Save the robot (the index may change)  	
    	TxObjectList objects = TxApplication.ActiveDocument.GetObjectsByName(robot_name);
    	var robot = objects[1] as TxRobot;
    	   	
    	// Create the new operation    	
    	TxContinuousRoboticOperationCreationData data = new TxContinuousRoboticOperationCreationData(op_name + op_idx);
    	TxApplication.ActiveDocument.OperationRoot.CreateContinuousRoboticOperation(data);

		// Get the created operation (by targeting its name)
        TxContinuousRoboticOperation MyOp = TxApplication.ActiveDocument.GetObjectsByName(op_name + op_idx)[0] as TxContinuousRoboticOperation;

        // Create all the necessary points       
        TxRoboticViaLocationOperationCreationData Point1 = new TxRoboticViaLocationOperationCreationData();
        Point1.Name = "point1op" + op_idx; // First point
        
        TxRoboticViaLocationOperationCreationData Point2 = new TxRoboticViaLocationOperationCreationData();
        Point2.Name = "point2op" + op_idx; // Second point
        
        TxRoboticViaLocationOperationCreationData Point3 = new TxRoboticViaLocationOperationCreationData();
        Point3.Name = "point3op" + op_idx; // Third point

		TxRoboticViaLocationOperationCreationData Point4 = new TxRoboticViaLocationOperationCreationData();
        Point4.Name = "point4op" + op_idx; // Fourth point

		TxRoboticViaLocationOperationCreationData Point5 = new TxRoboticViaLocationOperationCreationData();
        Point5.Name = "point5op" + op_idx; // Fifth point

		TxRoboticViaLocationOperationCreationData Point6 = new TxRoboticViaLocationOperationCreationData();
        Point6.Name = "point6op" + op_idx; // Sixth point

		TxRoboticViaLocationOperationCreationData Point7 = new TxRoboticViaLocationOperationCreationData();
        Point7.Name = "point7op" + op_idx; // Seventh point

		TxRoboticViaLocationOperationCreationData Point8 = new TxRoboticViaLocationOperationCreationData();
        Point8.Name = "point8op" + op_idx; // Eighth point

        TxRoboticViaLocationOperation FirstPoint = MyOp.CreateRoboticViaLocationOperation(Point1);
        TxRoboticViaLocationOperation SecondPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point2, FirstPoint);
        TxRoboticViaLocationOperation ThirdPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point3, SecondPoint);
		TxRoboticViaLocationOperation FourthPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point4, ThirdPoint);
		TxRoboticViaLocationOperation FifthPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point5, FourthPoint);
		TxRoboticViaLocationOperation SixthPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point6, FifthPoint);
		TxRoboticViaLocationOperation SeventhPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point7, SixthPoint);
		TxRoboticViaLocationOperation EighthPoint = MyOp.CreateRoboticViaLocationOperationAfter(Point8, SeventhPoint);

        // Put the first and the last point in the current robot TCP position (the correct frame)	
		TxFrame TCPpose1 = TxApplication.ActiveDocument.
		GetObjectsByName(new_tcp)[0] as TxFrame;
		var TCP_pose1 = new TxTransformation(TCPpose1.LocationRelativeToWorkingFrame);

        FirstPoint.LocationRelativeToWorkingFrame = TCP_pose1;
		EighthPoint.LocationRelativeToWorkingFrame = TCP_pose1;

        // Impose a position to the second waypoint	(acquire the position of the object to be picked)
		TxObjectList obj_pick = TxApplication.ActiveSelection.GetItems();
		obj_pick = TxApplication.ActiveDocument.GetObjectsByName(pick_target + op_idx);
		var obj = obj_pick[0] as ITxLocatableObject;
		var position_pick = new TxTransformation(obj.AbsoluteLocation);

		double rotVal2 = Math.PI;
		TxTransformation rotX2 = new TxTransformation(new TxVector(rotVal2, 0, 0), 
		TxTransformation.TxRotationType.RPY_XYZ);
		SecondPoint.AbsoluteLocation = rotX2;
		
		var pointB = new TxTransformation(SecondPoint.AbsoluteLocation);
		pointB.Translation = new TxVector(position_pick[0, 3], position_pick[1, 3], position_pick[2, 3] + 100);
		SecondPoint.AbsoluteLocation = pointB;
		
		// Impose a position to the third waypoint		
		double rotVal3 = Math.PI;
		TxTransformation rotX3 = new TxTransformation(new TxVector(rotVal3, 0, 0), 
		TxTransformation.TxRotationType.RPY_XYZ);
		ThirdPoint.AbsoluteLocation = rotX3;
		
		var pointC = new TxTransformation(ThirdPoint.AbsoluteLocation);
		pointC.Translation = new TxVector(position_pick[0, 3], position_pick[1, 3], position_pick[2, 3]);
		ThirdPoint.AbsoluteLocation = pointC;

		// Impose a position to the fourth waypoint		
		double rotVal4 = Math.PI;
		TxTransformation rotX4 = new TxTransformation(new TxVector(rotVal4, 0, 0), 
		TxTransformation.TxRotationType.RPY_XYZ);
		FourthPoint.AbsoluteLocation = rotX4;
		
		var pointD = new TxTransformation(FourthPoint.AbsoluteLocation);
		pointD.Translation = new TxVector(position_pick[0, 3], position_pick[1, 3], position_pick[2, 3] + 100);
		FourthPoint.AbsoluteLocation = pointD;

		// Impose a position to the fifth waypoint	(acquire the position of the object to be picked)
		TxObjectList obj_place = TxApplication.ActiveSelection.GetItems();
		obj_place = TxApplication.ActiveDocument.GetObjectsByName(place_target);
		var obj_pl = obj_place[0] as ITxLocatableObject;
		var position_place = new TxTransformation(obj_pl.AbsoluteLocation);

		double rotVal5 = Math.PI;
		TxTransformation rotX5 = new TxTransformation(new TxVector(rotVal5, 0, 0), 
		TxTransformation.TxRotationType.RPY_XYZ);
		FifthPoint.AbsoluteLocation = rotX5;
		
		var pointE = new TxTransformation(FifthPoint.AbsoluteLocation);
		pointE.Translation = new TxVector(position_place[0, 3], position_place[1, 3], position_place[2, 3] + 100);
		FifthPoint.AbsoluteLocation = pointE;

		// Impose a position to the sixth waypoint		
		double rotVal6 = Math.PI;
		TxTransformation rotX6 = new TxTransformation(new TxVector(rotVal6, 0, 0), 
		TxTransformation.TxRotationType.RPY_XYZ);
		SixthPoint.AbsoluteLocation = rotX6;
		
		var pointF = new TxTransformation(SixthPoint.AbsoluteLocation);
		pointF.Translation = new TxVector(position_place[0, 3], position_place[1, 3], position_place[2, 3]);
		SixthPoint.AbsoluteLocation = pointF;

		// Impose a position to the seventh waypoint		
		double rotVal7 = Math.PI;
		TxTransformation rotX7 = new TxTransformation(new TxVector(rotVal7, 0, 0), 
		TxTransformation.TxRotationType.RPY_XYZ);
		SeventhPoint.AbsoluteLocation = rotX7;
		
		var pointG = new TxTransformation(SeventhPoint.AbsoluteLocation);
		pointG.Translation = new TxVector(position_place[0, 3], position_place[1, 3], position_place[2, 3] + 100);
		SeventhPoint.AbsoluteLocation = pointG;

        // NOTE: you must associate the robot to the operation!
		MyOp.Robot = robot; 

		// Implement the logic to access the parameters of the controller		
		TxOlpControllerUtilities ControllerUtils = new TxOlpControllerUtilities();		
		TxRobot AssociatedRobot = ControllerUtils.GetRobot(MyOp); // Verify the correct robot is associated 
				
		ITxOlpRobotControllerParametersHandler paramHandler = (ITxOlpRobotControllerParametersHandler)
		ControllerUtils.GetInterfaceImplementationFromController(robot.Controller.Name,
		typeof(ITxOlpRobotControllerParametersHandler), typeof(TxRobotSimulationControllerAttribute),
		"ControllerName");

		// Set the new parameters for the waypoint					
		paramHandler.OnComplexValueChanged("Tool", "tcp_1", FirstPoint);
		paramHandler.OnComplexValueChanged("Motion Type", new_motion_type, FirstPoint);
        paramHandler.OnComplexValueChanged("Speed", new_speed, FirstPoint);
        paramHandler.OnComplexValueChanged("Accel", new_accel, FirstPoint);
		paramHandler.OnComplexValueChanged("Blend", new_blend, FirstPoint);
		paramHandler.OnComplexValueChanged("Coord Type", new_coord, FirstPoint);
		
		paramHandler.OnComplexValueChanged("Tool", "tcp_1", SecondPoint);
		paramHandler.OnComplexValueChanged("Motion Type", new_motion_type, SecondPoint);
        paramHandler.OnComplexValueChanged("Speed", new_speed, SecondPoint);
        paramHandler.OnComplexValueChanged("Accel", new_accel, SecondPoint);
		paramHandler.OnComplexValueChanged("Blend", new_blend, SecondPoint);
		paramHandler.OnComplexValueChanged("Coord Type", new_coord, SecondPoint);
		
		paramHandler.OnComplexValueChanged("Tool", "tcp_1", ThirdPoint);
		paramHandler.OnComplexValueChanged("Motion Type", new_motion_type, ThirdPoint);
        paramHandler.OnComplexValueChanged("Speed", new_speed, ThirdPoint);
        paramHandler.OnComplexValueChanged("Accel", new_accel, ThirdPoint);
		paramHandler.OnComplexValueChanged("Blend", new_blend, ThirdPoint);
		paramHandler.OnComplexValueChanged("Coord Type", new_coord, ThirdPoint);

		paramHandler.OnComplexValueChanged("Tool", "tcp_1", FourthPoint);
		paramHandler.OnComplexValueChanged("Motion Type", new_motion_type, FourthPoint);
        paramHandler.OnComplexValueChanged("Speed", new_speed, FourthPoint);
        paramHandler.OnComplexValueChanged("Accel", new_accel, FourthPoint);
		paramHandler.OnComplexValueChanged("Blend", new_blend, FourthPoint);
		paramHandler.OnComplexValueChanged("Coord Type", new_coord, FourthPoint);

		paramHandler.OnComplexValueChanged("Tool", "tcp_1", FifthPoint);
		paramHandler.OnComplexValueChanged("Motion Type", new_motion_type, FifthPoint);
        paramHandler.OnComplexValueChanged("Speed", new_speed, FifthPoint);
        paramHandler.OnComplexValueChanged("Accel", new_accel, FifthPoint);
		paramHandler.OnComplexValueChanged("Blend", new_blend, FifthPoint);
		paramHandler.OnComplexValueChanged("Coord Type", new_coord, FifthPoint);

		paramHandler.OnComplexValueChanged("Tool", "tcp_1", SixthPoint);
		paramHandler.OnComplexValueChanged("Motion Type", new_motion_type, SixthPoint);
        paramHandler.OnComplexValueChanged("Speed", new_speed, SixthPoint);
        paramHandler.OnComplexValueChanged("Accel", new_accel, SixthPoint);
		paramHandler.OnComplexValueChanged("Blend", new_blend, SixthPoint);
		paramHandler.OnComplexValueChanged("Coord Type", new_coord, SixthPoint);

		paramHandler.OnComplexValueChanged("Tool", "tcp_1", SeventhPoint);
		paramHandler.OnComplexValueChanged("Motion Type", new_motion_type, SeventhPoint);
        paramHandler.OnComplexValueChanged("Speed", new_speed, SeventhPoint);
        paramHandler.OnComplexValueChanged("Accel", new_accel, SeventhPoint);
		paramHandler.OnComplexValueChanged("Blend", new_blend, SeventhPoint);
		paramHandler.OnComplexValueChanged("Coord Type", new_coord, SeventhPoint);

		paramHandler.OnComplexValueChanged("Tool", "tcp_1", EighthPoint);
		paramHandler.OnComplexValueChanged("Motion Type", new_motion_type, EighthPoint);
        paramHandler.OnComplexValueChanged("Speed", new_speed, EighthPoint);
        paramHandler.OnComplexValueChanged("Accel", new_accel, EighthPoint);
		paramHandler.OnComplexValueChanged("Blend", new_blend, EighthPoint);
		paramHandler.OnComplexValueChanged("Coord Type", new_coord, EighthPoint);

		// Add OLP commands
		AddOpenCloseCommand("point3op" + op_idx, "Camozzi Gripper UR5e", "CLOSE", "tf_tcp_1");
		AddOpenCloseCommand("point6op" + op_idx, "Camozzi Gripper UR5e", "OPEN", "tf_tcp_1");
        
    }

	static TxObjectList GetCreatedOperations(bool verbose, StringWriter m_output)
	{
		TxOperationRoot variable = TxApplication.ActiveDocument.OperationRoot;
    	
    	// Specify the type of operations that we want to get
    	TxTypeFilter filter = new TxTypeFilter(typeof(ITxOperation));
        filter.AddIncludedType(typeof(TxHumanTsbSimulationOperation));
    	filter.AddIncludedType(typeof(TxContinuousRoboticOperation));
    	
    	// Get the list of operations: do not use 'GetAllDescendants' because it also gives the points inside the operations
    	TxObjectList List = variable.GetDirectDescendants(filter);

		if (verbose)
		{
			// Display the names to check if they are correct
			for (int ii = 0; ii < List.Count; ii ++)
			{
				int new_idx = ii + 1;
				m_output.Write("The operation number " + new_idx + " is called: " + List[ii].Name.ToString() + "\n");
    		}
		}

		return List;
	}

	static void CreateGantt(TxObjectList created_op, int[,] sequence, int[,] starting_times, 
							int multiplier, StringWriter m_output, bool verbose)
	{
		// Define some variables
    	string comp_op_name = "CompOp";

		// Create the compound operation and save it in a variable
        TxCompoundOperationCreationData dat = new TxCompoundOperationCreationData(comp_op_name);
        TxApplication.ActiveDocument.OperationRoot.CreateCompoundOperation(dat);
    
    	// Get the compound operation    	
        TxObjectList Operation = TxApplication.ActiveDocument.GetObjectsByName(comp_op_name);
        var op = Operation[0] as ITxCompoundOperation;
        
        TxCompoundOperation comp_op = Operation[0] as TxCompoundOperation;

		m_output.Write("The name of the compound operation is: " + comp_op.Name.ToString() + "\n");

		// Add operations to the complete one and then create the Gantt chart
		for (int ii = 0; ii < created_op.Count; ii ++)
		{
			int task_idx = sequence[0, ii];
			m_output.Write("The variable task_idx is: " + task_idx + "\n");
			string task_name = created_op[task_idx].Name.ToString();
			m_output.Write("The variable task_name is: " + task_name + "\n");
			TxObjectList Task_ii = TxApplication.ActiveDocument.GetObjectsByName(task_name);
			m_output.Write("MIAO4\n");
			var add_task_ii = Task_ii[0] as ITxObject;
			var task_ii = Task_ii[0] as ITxOperation;
			m_output.Write("The name of the task is: " + task_ii.Name.ToString() + "\n");
			double time_ii = (double)starting_times[0, ii] / multiplier;
			m_output.Write("The variable time_ii is: " + time_ii + "\n");

			comp_op.AddObject(add_task_ii);
			comp_op.SetChildOperationRelativeStartTime(task_ii, time_ii);
		}

		// If you arrived at this point, everything worked fine
		m_output.Write("The Gantt chart was created successfully!");
	}
}

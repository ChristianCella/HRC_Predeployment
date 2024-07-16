""" 
This is the first attempt for a structured communication.
* In Python, the sequence of operations is defined ("Who does what"): 0 = robot, 1 = human.
* In C# this array is received and the operations are created (type "Pick&Place" both for human and robot).
* Each Operation is run singularly, the time is calculated and the results are sent back to Python.
* Python mimics the "Scheduling" algorithm (not yet implemented), finds the optimal schedule and sends the times back to C#.
* In C# the times are received and a function concatenates the operations (Gantt chart).
* The complete collaborative operation is run and a boolean flag is sent to Python to notify possible collisions.
"""

import socket
import numpy as np

import sys
sys.path.append('.')

from src.utils.configuration import *
from src.Layout_optimizer import *
from src.Scheduler import *

def send_array(sock, array):
    # Send the shape and type of the array first
    shape = np.array(array.shape, dtype=np.int32)
    sock.sendall(shape.tobytes())
    sock.sendall(array.tobytes())

def main():

    # Create a socket object
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

    # Define the host and port
    host = '127.0.0.1'
    port = 12345

    # Connect to the server
    s.connect((host, port))

    # First send (shared data)
    shared_data = np.array([[Nsim, multiplier, Ntasks]], dtype = np.int32)
    send_array(s, shared_data)

    # Initialize the trigegr here (not in the configuration file)
    trigger_end = 0

    while trigger_end < Nsim - 1:

        # Create all the necessary arrays      
        sequence = np.array([branch_and_bound], dtype = np.int32)
        send_array(s, sequence)

        # receive the array of times for each operation
        kpi = s.recv(1024).decode()
        kpi = [int(num) for num in kpi.split(',')] # list variable
                  
        # Print the values          
        kpi_vec = np.array(kpi)

        for i in range(0, len(kpi_vec)):
            print(f"The execution time of operation {i} is: {kpi_vec[i]} second(s)")

        # Create the scheduling    
        scheduling = np.array([[x + trigger_end for x in milp]], dtype = np.int32)
        send_array(s, scheduling)

        # Receive the variable 'trigger_end' from C# code
        trigger_end = int(s.recv(1024).decode())
        print(f"Trigger end: {trigger_end}")

    # Close the connection
    s.close()

if __name__ == "__main__":

    # Run the code
    main()

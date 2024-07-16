""" 
Test1: more than one array is sent to C# (in sequence)
"""

import socket
import numpy as np

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

    # Number of simulations
    Nsim = 5
    trigger_end = 0

    while trigger_end < Nsim - 1:

        # Create two numpy arrays
        sequence = np.array([[0, 0, 1]], dtype = np.int32)
        shared = np.array([[2, 3]], dtype = np.int32)
        starting_times = np.array([[trigger_end, trigger_end + 1, trigger_end + 2]], dtype = np.int32)

        # Send arrays to the server
        send_array(s, sequence)
        send_array(s, shared)
        send_array(s, starting_times)

        # Receive the variable 'trigger_end' from C# code
        trigger_end = int(s.recv(1024).decode())
        print(f"Trigger end: {trigger_end}")

    # Close the connection
    s.close()

if __name__ == "__main__":

    # Run the code
    main()

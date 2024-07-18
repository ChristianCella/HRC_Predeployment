""" 
Test3: more than one array (both integers and strings) are sent to C#
"""

import socket
import numpy as np

def send_array(sock, array):

    # Send the shape and type of the array first
    shape = np.array(array.shape, dtype=np.int32)
    sock.sendall(shape.tobytes())
    sock.sendall(array.tobytes())

def send_string(sock, string_list):

    # Join the list of strings into a single string separated by commas
    data = ','.join(string_list)

    # Convert the string to bytes and send its length first
    data_bytes = data.encode()
    length = np.array([len(data_bytes)], dtype=np.int32)
    sock.sendall(length.tobytes())

    # Send the actual string data
    sock.sendall(data_bytes)

def main():

    # Initialize the socket communication
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    host = '127.0.0.1'
    port = 12345
    s.connect((host, port))

    Nsim = 5
    trigger_end = 0

    while trigger_end < Nsim - 1:

        # Create the vectors to be sent to C#
        sequence = np.array([[0, 0, 1]], dtype = np.int32)
        shared = np.array([[2, 3]], dtype = np.int32)
        starting_times = np.array([[trigger_end, trigger_end + 1, trigger_end + 2]], dtype = np.int32)
        string_data = ["first", "second", "third"]

        # Send all the data
        send_array(s, sequence)
        send_array(s, shared)
        send_array(s, starting_times)
        send_string(s, string_data)

        # Receive the trigger end from C#
        trigger_end = int(s.recv(1024).decode())
        print(f"Trigger end: {trigger_end}")

    s.close()

if __name__ == "__main__":
    main()

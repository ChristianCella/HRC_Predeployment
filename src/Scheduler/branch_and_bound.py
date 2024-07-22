import numpy as np

import sys
sys.path.append('.')
from src.utils.configuration import *


""" 
Here goes the TAS algorithm: 'branch_and_bound' and 'milp' will be given by the algorithm
"""

import numpy as np

class AllocationAndScheduling:
    def __init__(self, Ntasks):
        self.Ntasks = Ntasks

    def allocation(self):
        """
        This function will be the branch and bound algorithm
        """
        # List of tasks for the human
        human_idx = [1, 2]

        # Create the 'allocation' variable
        branch_and_bound = [0] * self.Ntasks  # empty list of zeros
        for idx in human_idx:
            branch_and_bound[idx] = 1

        return branch_and_bound

    def scheduling(self, times: np.ndarray):
        """
        This function will be the MILP algorithm
        """
        # Reorder the times in descending order
        sorted_indices = np.argsort(times)[::-1]
        sorted_times = times[sorted_indices]

        # Create the vector of starting times
        starting_times = np.zeros(len(times))
        sum_var = 0

        # Set the starting times based on the sorted order
        for i, index in enumerate(sorted_indices):
            if i == 0:
                starting_times[i] = 0  # The first task starts at 0
                sum_var = starting_times[i]
            else:
                starting_times[i] = sorted_times[i - 1] + sum_var
                sum_var = starting_times[i]

        # Convert sorted_indices to a list and return it along with starting_times
        return sorted_indices.tolist(), starting_times

# Example usage
if __name__ == "__main__":
    
    times = np.array([3.5, 2.1, 4.7, 1.8])
    analyzer = AllocationAndScheduling(Ntasks)
    sorted_indices, starting_times = analyzer.scheduling(times)
    print("Sorted Indices:", sorted_indices)  # Output the indices in decreasing order of the times array
    print("Starting Times:", starting_times)  # Output the correct starting times



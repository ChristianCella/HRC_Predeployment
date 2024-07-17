import sys
sys.path.append('.')
from src.utils.configuration import *

""" 
Here goes the TAS algorithm: 'branch_and_bound' and 'milp' will be given by the algorithm
"""

# List of tasks for the human
human_idx = [1, 2]

# Create the 'allocation' variable
branch_and_bound = [0] * Ntasks # empty list of zeros
for idx in human_idx:
    branch_and_bound[idx] = 1

# Create the 'sequence' variable (this will contain the statring times for the operations)
milp = [2, 3, 4, 5]
  
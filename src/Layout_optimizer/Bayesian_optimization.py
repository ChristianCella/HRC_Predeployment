import sys
sys.path.append('.')
from src.utils.configuration import *

""" 
For the moment, tehse positions are fixed; in the end, this will be the output of the optimization algorithm
"""

x1 = 400
y1 = 0 
z1 = 25
rx1 = 0
ry1 = 0
rz1 = 0

x2 = 350
y2 = -300
z2 = 25
rx2 = 0
ry2 = 0
rz2 = 0

x3 = 600
y3 = -300
z3 = 25
rx3 = 0
ry3 = 0
rz3 = 0

x4 = 550
y4 = 200
z4 = 25
rx4 = 0
ry4 = 0
rz4 = 0

# Create the list
disposition = [x1, y1, z1, rx1, ry1, rz1, x2, y2, z2, rx2, ry2, rz2, x3, y3, z3, rx3, ry3, rz3, x4, y4, z4, rx4, ry4, rz4]
layout = [round(element, Ndecimals) * multiplier for element in disposition]

print(f"The layout is: {layout}")
  
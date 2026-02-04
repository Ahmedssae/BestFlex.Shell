import os

# Define the path to the solution file
solution_path = "C:\\Users\\Ahmed.salah\\Documents\\BestFlex.Shell\\BestFlex.Shell.sln"

# Read the contents of the solution file
with open(solution_path, 'r') as f:
    solution_contents = f.read()

# Modify the solution contents (if needed)
new_project_reference = "<ProjectReference Include=\"BestFlex.Shell\" />"
solution_contents += "\n" + new_project_reference

# Write the modified solution contents back to the file
with open(solution_path, 'w') as f:
    f.write(solution_contents)

print("Solution file modified successfully.")
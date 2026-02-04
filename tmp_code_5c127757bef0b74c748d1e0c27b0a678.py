import os

project_path = r"C:\Users\Ahmed.salah\Documents\BestFlex.Shell"
for root, dirs, files in os.walk(project_path):
    for file in files:
        if file.endswith(".cs"):
            print(os.path.join(root, file))
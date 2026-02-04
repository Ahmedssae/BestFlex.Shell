import os
import filecmp

# Define the path to the New Sale screen files
new_sale_screen_path = r'C:\Path\To\BestFlex\Project\Views\NewSaleScreen.xaml'

# Define a list of unused fake placeholders (to be confirmed)
unused_placeholders = ['Placeholder1', 'Placeholder2']

def remove_unused_placeholders(file_path):
    with open(file_path, 'r') as file:
        content = file.read()
    
    for placeholder in unused_placeholders:
        if placeholder in content:
            # Remove the placeholder from the content
            content = content.replace(placeholder, '')
    
    with open(file_path, 'w') as file:
        file.write(content)

# Check if any changes were made to the New Sale screen files
def check_changes(original_file, modified_file):
    return not filecmp.cmp(original_file, modified_file)

# Remove unused placeholders from the New Sale screen files
remove_unused_placeholders(new_sale_screen_path)

# Check for changes and notify if any were made
if check_changes(os.path.join(new_sale_screen_path + '.original'), new_sale_screen_path):
    print('Changes made to the New Sale screen files.')
else:
    print('No changes made to the New Sale screen files.')

# Run dotnet build to validate the modifications
print('Running dotnet build...')
os.system('dotnet build')
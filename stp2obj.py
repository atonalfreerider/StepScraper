import os
import Mesh
import MeshPart
import Import

# Retrieve the input filename from the environment variable
input_filename = os.getenv('STP_FILE_PATH')

if not input_filename:
    print("Error: STP_FILE_PATH environment variable is not set.")
    sys.exit(1)

# Try opening the file and processing it
try:
    data = Import.open(input_filename)
    if not data:
        print("No data could be loaded from the file. Please check the file format and content.")
        sys.exit(1)

    shape = data[0][0].Shape
    mesh = MeshPart.meshFromShape(Shape=shape, LinearDeflection=0.1, Segments=True)
    output_filename = input_filename.rsplit('.', 1)[0] + '.obj'
    mesh.write(Filename=output_filename, Format="obj")

except Exception as e:
    print(f"Failed to open file: {input_filename}")
    print(f"Error: {e}")
    sys.exit(1)

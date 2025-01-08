import os
import unreal # type: ignore
import argparse
import sys
import json

def main():
   files_to_exclude = sys.argv[4]
   if files_to_exclude != "None":
       with open(files_to_exclude, 'r') as f:
           files_to_exclude = json.load(f)
   else:
       files_to_exclude = []
   
   export_assets(
       output_folder=sys.argv[1], 
       meshes_source_directory=sys.argv[2], 
       textures_source_directory=sys.argv[3],
       files_to_exclude=files_to_exclude
   )

def should_skip_file(export_path, files_to_exclude):
   return any(os.path.normpath(export_path) == os.path.normpath(exclude) for exclude in files_to_exclude)

def export_assets(output_folder, meshes_source_directory, textures_source_directory, files_to_exclude):
   export_meshes = meshes_source_directory != "None"
   export_textures = textures_source_directory != "None"
  
   project_content_dir = unreal.SystemLibrary.get_project_content_directory()
   print(f"Project content directory: {project_content_dir}")

   if export_meshes:
       meshes_output_directory = os.path.join(output_folder, "Meshes")
       os.makedirs(meshes_output_directory, exist_ok=True)
       meshes_directory = os.path.join(project_content_dir, *meshes_source_directory.split("\\"))
       print(f"Processing meshes from: {meshes_directory}")
       export_meshes_from_directory(meshes_directory, meshes_output_directory, files_to_exclude)

   if export_textures:
       textures_output_directory = os.path.join(output_folder, "Textures")
       os.makedirs(textures_output_directory, exist_ok=True)
       textures_directory = os.path.join(project_content_dir, *textures_source_directory.split("\\"))
       print(f"Processing textures from: {textures_directory}")
       export_textures_from_directory(textures_directory, textures_output_directory, files_to_exclude)

def export_meshes_from_directory(source_directory, output_directory, files_to_exclude):
   print(f"Scanning for meshes in: {source_directory}")
   for root, dirs, files in os.walk(source_directory):
       for file in files:
           if file.endswith(".uasset"):
               export_path = os.path.join(output_directory, file.replace(".uasset", ".fbx"))
               if should_skip_file(export_path, files_to_exclude):
                   print(f"Skipping excluded file: {export_path}")
                   continue

               asset_path = os.path.join(root, file)
               asset_path_in_unreal = convert_path_to_unreal(asset_path)
               print(f"Found mesh asset: {asset_path_in_unreal}")

               asset = unreal.EditorAssetLibrary.load_asset(asset_path_in_unreal)
               if not asset:
                   print(f"Failed to load mesh asset: {asset_path_in_unreal}")
                   continue

               export_task = unreal.AssetExportTask()
               export_task.object = asset
               export_task.filename = export_path
               export_task.automated = True
               export_task.replace_identical = True
               export_task.prompt = False
               export_task.selected = False

               fbx_export_options = unreal.FbxExportOption()
               fbx_export_options.fbx_export_compatibility = unreal.FbxExportCompatibility.FBX_2020
               fbx_export_options.ascii = False
               fbx_export_options.force_front_x_axis = False
               fbx_export_options.vertex_color = True
               fbx_export_options.level_of_detail = True
               fbx_export_options.collision = False
               fbx_export_options.export_source_mesh = True
               fbx_export_options.export_morph_targets = True
               fbx_export_options.export_preview_mesh = False
               fbx_export_options.map_skeletal_motion_to_root = False
               fbx_export_options.export_local_time = False

               export_task.options = fbx_export_options

               success = unreal.Exporter.run_asset_export_task(export_task)
               if success:
                   print(f"Successfully exported mesh to: {export_task.filename}")
               else:
                   print(f"Failed to export mesh: {asset_path_in_unreal}")

def export_textures_from_directory(source_directory, output_directory, files_to_exclude):
   print(f"Scanning for textures in: {source_directory}")
   for root, dirs, files in os.walk(source_directory):
       for file in files:
           if file.endswith(".uasset"):
               asset_path = os.path.join(root, file)
               asset_path_in_unreal = convert_path_to_unreal(asset_path)
               print(f"Found texture asset: {asset_path_in_unreal}")

               asset = unreal.EditorAssetLibrary.load_asset(asset_path_in_unreal)
               if not asset or not isinstance(asset, unreal.Texture):
                   print(f"Failed to load or invalid texture asset: {asset_path_in_unreal}")
                   continue

               file_extension = "png"
               import_data = asset.get_editor_property('asset_import_data')
               if import_data:
                   source_file_path = import_data.get_first_filename()
                   if source_file_path:
                       file_extension = source_file_path.split('.')[-1].lower()
                       print(f"Source file format: {file_extension}")

               extension = ".dds" if file_extension == "dds" else ".png"
               export_path = os.path.join(output_directory, file.replace(".uasset", extension))
               
               if should_skip_file(export_path, files_to_exclude):
                   print(f"Skipping excluded file: {export_path}")
                   continue

               export_task = unreal.AssetExportTask()
               export_task.object = asset
               export_task.filename = export_path
               export_task.automated = True
               export_task.replace_identical = True
               export_task.prompt = False
               export_task.selected = False

               success = unreal.Exporter.run_asset_export_task(export_task)
               if success:
                   print(f"Successfully exported texture to: {export_task.filename}")
               else:
                   print(f"Failed to export texture: {asset_path_in_unreal}")

def convert_path_to_unreal(file_path):
   content_index = file_path.lower().find("content")
   if content_index == -1:
       raise ValueError(f"File path does not contain 'Content': {file_path}")
   
   relative_path = file_path[content_index + len("Content"):]
   unreal_path = "/Game" + relative_path.replace("\\", "/").replace(".uasset", "")
   print(f"Converted path: {file_path} -> {unreal_path}")
   return unreal_path

if __name__ == "__main__":
   main()
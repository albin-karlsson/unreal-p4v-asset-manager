<<<<<<< HEAD
# Wormhole v1.0

## Overview

The Worm Works Unreal exporter **Wormhole** is a simple tool that integrates with Unreal Engine, automating the export of assets from Unreal and uploading them to Perforce. 

## Features

- **Launch Unreal Engine**: Automatically opens your Unreal Engine project and runs a Python script to export assets.
- **Texture conversion**: Automatically converts all exported PNG textures into DDS format. 
- **Perforce integration**: Uploads the exported assets directly to your Perforce server, ensuring your work is safely version-controlled.

## Getting started

### Prerequisites

Before using the tool, make sure you have the following installed:

- **Unreal Engine**: Installed on your machine. The tool will automatically launch the Unreal Editor.
- **Perforce client**: Installed and configured on your machine. The tool will upload assets to your Perforce server.

### Using the tool

1. **Launch the tool**: Start the program by running the **Wormhole v\*** shortcut.
2. **Fill username and password**: Carefully in your Perforce username as well as your Perforce password.
3. **Browse for Unreal**: Browse for UnrealEditor.exe on your local machine, or click the checkbox to use the default location.
4. **Get perforce workspaces**: Double check that you have filled in your Perforce username and password and then get all Perforce workspaces by clicking the **Get workspaces** button. 
5. **Export assets**: Click the **Export assets** button.
6. **Wait for the process**: A **Command prompt** window will appear while the script for converting all PNG files to DDS format. Just **press Enter** when prompted to proceed. The tool will automatically export all meshes and textures from Unreal and push them to Perforce.
=======
# unreal-p4v-asset-manager
>>>>>>> 5d6d5eac6d0f5ad5e24978277f4390672a9de6bb

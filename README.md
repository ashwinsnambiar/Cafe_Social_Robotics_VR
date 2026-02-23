# Cafe Social Robotics VR

This repository contains a Unity project for simulating a social robot in a virtual reality (VR) cafe environment. The project is designed to explore human-robot interaction scenarios using a realistic cafe setup and an Agibot G1 robot.

## Project Overview
- The cafe environment is based on the LearesStudio asset, downloaded and set up in Unity.
- The Agibot G1 robot model was sourced from [GenieSimAssets on HuggingFace](https://huggingface.co/datasets/agibot-world/GenieSimAssets), converted to FBX, and imported into Unity.
- Primitive collision bodies were manually added for each robot link, and articulation bodies were configured for all joints according to USD limits.
- A NavMesh and NavAgent were implemented to enable autonomous robot navigation within the cafe.
- The bar setup includes bakery and burger assets, allowing VR operators or subjects to interact with objects using grab and place mechanics. Doors can be opened and closed.
- A civilian female model was added, with Mixamo animations used for background cafe customers.

## Features
- Realistic VR cafe environment
- Agibot G1 social robot simulation
- Autonomous navigation using NavMesh
- Interactive bar setup with bakery and burger assets
- VR hand grab and place mechanics
- Animated background customers

## Asset Credits
See [credits.md](./credits.md) for a full list of third-party assets used in this project.

## Agibot G1 Robot License and Citation
The Agibot G1 robot model is used under the terms specified by the [GenieSimAssets dataset](https://huggingface.co/datasets/agibot-world/GenieSimAssets#license-and-citation). Please cite as follows:

> GenieSimAssets: Agibot G1 robot model. Downloaded from https://huggingface.co/datasets/agibot-world/GenieSimAssets. See license and citation details [here](https://huggingface.co/datasets/agibot-world/GenieSimAssets#license-and-citation).

## Getting Started
1. Clone this repository.
2. Open the project in Unity (recommended version: Unity 6.3 LTS or later).
3. Ensure VR support is enabled in your Unity Editor by using the proper XR Interaction Toolkit package and OpenXR plugin.
4. Play the scene to explore the cafe and interact with the robot.

## License
See individual asset licenses in [credits.md](./credits.md). Project code is licensed under the MIT License unless otherwise specified.

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
- Realistic VR cafe environment with physics-based kitchen & bakery items
- Agibot G1 social robot simulation with autonomous navigation and dual-arm manipulation
- Centralized **Master Experiment Control Hub** (`ExperimentSessionManager`) for 1-click trial setup
- Multi-set order workflow ($N$ Sets $\times$ $M$ Orders) with patient nutritional constraint checking
- Low vs. High cognitive workload condition toggles
- Distraction event triggers (broken bottle crashes, customer coffee spills) with robot interrupt & cleanup sequences
- Comprehensive experimenter host controls & automated CSV / JSON trial data logging
- Smooth low-light VR rest breaks and clean inter-set scene transitions

## Asset Credits
See [credits.md](./credits.md) for a full list of third-party assets used in this project.

## Agibot G1 Robot License and Citation
The Agibot G1 robot model is used under the terms specified by the [GenieSimAssets dataset](https://huggingface.co/datasets/agibot-world/GenieSimAssets#license-and-citation). Please cite as follows:

> GenieSimAssets: Agibot G1 robot model. Downloaded from https://huggingface.co/datasets/agibot-world/GenieSimAssets. See license and citation details [here](https://huggingface.co/datasets/agibot-world/GenieSimAssets#license-and-citation).

## Getting Started
1. Clone this repository.
2. Open the project in Unity (recommended version: Unity 6.3 LTS or later).
3. Ensure VR support is enabled in your Unity Editor by using the proper XR Interaction Toolkit package and OpenXR plugin.
4. Select the **`[ExperimentSessionManager]`** GameObject in the Hierarchy to configure all trial parameters (orders, tables, patient nutritional limits, spawn intervals, host event keys).
5. For complete documentation on setting up experiments, hotkeys, and orders, refer to the [EXPERIMENT_AND_ORDER_SETUP_GUIDE.md](./EXPERIMENT_AND_ORDER_SETUP_GUIDE.md).
6. Press **Play** in Unity to run the experiment.

## License
See individual asset licenses in [credits.md](./credits.md). Project code is licensed under the MIT License unless otherwise specified.

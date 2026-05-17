Revised Agent Prompt: The "Area of Effect" Implementation
Role: Senior CS2 Modding Engineer.
Context: You are developing a mod called "Area of Effect" for Cities: Skylines 2. The project structure is already initialized in C:\Users\ajord\source\repos\Area_of_Effect\.

Step 1: Process Documentation (Mandatory)
Before writing code, analyze these specific technical references to understand the CS2 implementation of ECS and Overlay Rendering:

Core Architecture: ECS - Entity Component System

System Catalog: Systems and Components Catalog

Rendering Circles: Creating a Tool (Specifically the section on visual overlays).

Startup/Setup: ps1ke Modding Guide for terminal debugging setup.

Step 2: Technical Logic Path

Read the Types: Scan the project's referenced assemblies (via .csproj) to locate the Game.Prefabs and Game.Rendering namespaces.

Identify the Target: Focus on the Radius component and ServiceCoverageData.

Implementation:

Create AreaOfEffectSystem.cs.

Inherit from Game.Systems.GameSystemBase.

Use SystemAPI.GetSingleton<ToolSystem>() to find the selectedEntity.

Use OverlayRenderSystem.Buffer to draw the circle.

Step 3: Debugging Protocol

Implement logging using the game's native log system as detailed in Logging.

Ensure the terminal launches on startup per the ps1ke guide so we can see the entity IDs being queried in real-time.

Instructions for the Agent:
"Do not hallucinate class names. If a component name is uncertain, provide a placeholder and ask me to verify it against the Systems and Components catalog link provided above. Start by generating the AreaOfEffectSystem.cs file logic first."
# Ilias Precession Simulation

This repository contains the 3D gyroscope simulation for the ilias-precession-module.

##	Milestons + what to learn
- 1	Look around the editor: place a few cubes and cylinders, move the camera, press Play	Scene, Hierarchy, Inspector and Project windows; GameObjects; Transforms; parent/child relationships
- 2	Build the toy gyroscope from basic shapes: rotor disk, frame ring, axle, a stand with a pivot	Hierarchy as a tree of coordinate frames, local vs. world coordinates, materials, prefabs
- 3	Your first script: make the rotor spin at a constant rate	C# classes, MonoBehaviour, Update(), Time.deltaTime, Inspector fields, Debug.Log
- 4	Prescribed precession: move the axis along a cone at the rate from the theory formula Ω = mgr/(I₃ω₃), with no equations of motion yet	Rotation order, quaternions in Unity, the left-handed coordinate system
- 5	Physics model: the Lagrangian of the heavy top, then explicit Euler, then RK4, with an energy readout	Plain C# classes, struct vs class, double vs float, FixedUpdate()
- 6	Connect the model to the view	Separating logic from presentation; converting between your physics frame and Unity's frame
- 7	Vector arrows for L, ω and τ, plus a trace of the axis tip	LineRenderer, creating objects at runtime, references between components
- 8	Controls: sliders for spin rate, mass and lever arm, plus readouts	Unity UI, events/callbacks
- 9	Web build and embedding in the Angular module	Build settings, Web-specific settings, serving the files with Apache
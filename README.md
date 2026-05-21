# SOCCER-SAMUR-AI-ROBOT

## Vision-Based Reinforcement Learning for Differential Drive Soccer Robots

SOCCER-SAMUR-AI-ROBOT is an experimental robotics and artificial intelligence project focused on training autonomous soccer robots using reinforcement learning inside Unity ML-Agents.

The system intentionally avoids privileged simulator information during training and instead relies on a YOLO-inspired visual perception pipeline, simulating how a real-world robot would perceive the environment through a camera.

The project explores the intersection between:

- Reinforcement Learning
- Robotic Soccer
- Differential Drive Locomotion
- Computer Vision
- Embedded Robotics
- Human Strategy Modeling
- Emergent Competitive Behaviors

---

# Core Philosophy

Most simulated robotics projects cheat.

They use direct world coordinates, perfect distances, instant object detection, or omniscient environment data that real robots never possess.

This project takes the opposite direction.

The agent learns almost entirely from camera perception.

The robot does not "know" where the ball is in world space.

Instead, it receives noisy perception signals similar to a real embedded robotic system using:

- Object detection
- Bounding box positioning
- Relative visual scale
- Camera framing
- Goal visibility
- Screen-space alignment

The objective is to create agents capable of transferring more naturally into physical robotics systems.

---

# Key Features

## YOLO-Style Vision Mock

The environment simulates a lightweight computer vision pipeline.

The robot perceives:

- Ball visibility
- Ball screen position
- Ball apparent size
- Goal visibility
- Goal screen position
- Goal apparent size

All values are normalized in viewport coordinates.

```csharp
ballX
ballY
ballSize
goalX
goalY
goalSize
```

---

## Differential Drive Physics

The robot uses independent left/right motor control.

```text
leftMotor
rightMotor
```

This mimics:

- Tank drive robots
- RC robotic platforms
- Embedded wheel control systems
- Real soccer robotics hardware

Movement emerges from motor asymmetry rather than idealized navigation commands.

---

## Curriculum Learning

Training is divided into progressive behavioral stages.

| Course | Objective |
|---|---|
| 0 | Detect ball and goal |
| 1 | Approach the ball |
| 2 | Align attack trajectory |
| 3 | Touch the ball |
| 4 | Push ball toward goal |
| 5 | Aim shots |
| 6 | Score goals |

This staged curriculum stabilizes learning and accelerates policy convergence.

---

# Attack Alignment System

One of the main innovations of the project is the concept of **visual attack alignment**.

The robot is rewarded only when:

1. The ball is visible
2. The enemy goal is visible
3. The goal appears visually behind the ball

This creates emergent offensive positioning behaviors.

Instead of blindly chasing the ball, the robot learns to:

- Rotate strategically
- Approach from advantageous angles
- Avoid own-goals
- Reposition before pushing

The alignment is computed entirely in image space.

```csharp
float dx = Mathf.Abs(ballX - goalX);

attackAlignment = 1f - dx;
```

This is intentionally closer to how real robotic vision systems reason about attack geometry.

---

# Realistic Vision Constraints

The environment contains intentional perceptual limitations.

For example:

- The goal becomes invisible from behind
- The robot can lose visual tracking
- Alignment depends on camera framing
- Rewards depend on perception consistency

This creates more realistic robotic behaviors and prevents simulator exploits.

---

# Reinforcement Learning Stack

## Engine

- Unity 3D
- Unity ML-Agents

## ML Backend

- PyTorch

## Learning Method

- PPO (Proximal Policy Optimization)

---

# Intended Real-World Transfer

The architecture was designed with physical robotics deployment in mind.

Future hardware targets include:

- Raspberry Pi robots
- ESP32 robotic platforms
- Smartphone-powered robots
- Jetson Nano systems
- RC soccer robots
- Autonomous educational robotics

---

# Debug Visualization

The project includes real-time debug instrumentation.

Visual feedback includes:

- Camera rays
- Attack alignment lines
- Reward flashes
- Bounding box overlays
- Motor PWM visualization

Green debug lines indicate positive reward events.

This creates an intuitive understanding of what the agent is learning internally.

---

# Example Training Metrics

```text
[INFO] SoccerAgent
Step: 150000
Mean Reward: 3.804
Std Reward: 1.510
```

Emergent behaviors observed include:

- Ball pursuit
- Goal-oriented approach
- Offensive repositioning
- Attack alignment correction
- Reduced self-goaling
- Ball trapping behaviors

---

# Future Research Directions

## Planned Features

- Multi-agent team coordination
- Human strategy imitation
- Neural tactical memory
- Adversarial robot leagues
- Sim2Real transfer pipelines
- Real YOLO integration
- Hardware deployment
- Self-play tournaments
- Vision transformers
- Swarm coordination

---

# Installation

## Requirements

- Unity 2022+
- Unity ML-Agents
- Python 3.10+
- PyTorch
- CUDA (optional)

---

# ML-Agents Setup

```bash
pip install mlagents
pip install torch
```

---

# Training

```bash
mlagents-learn config/ppo/Soccer.yaml --run-id=samurai_run
```

Then press Play inside Unity.

---

# Repository Structure

```text
SOCCER-SAMUR-AI-ROBOT/
│
├── Assets/
│   ├── Scripts/
│   ├── Scenes/
│   ├── Prefabs/
│   └── Materials/
│
├── config/
│   └── ppo/
│
├── models/
│
├── results/
│
└── README.md
```

---

# Research Motivation

This project explores a broader question:

> Can competitive play become a primary domain for embodied artificial intelligence?

Instead of optimizing robots for labor automation alone, the system investigates how strategy, movement, play, and competition can become emergent intelligence training grounds.

Robotic soccer becomes a compressed simulation of:

- Navigation
- Prediction
- Cooperation
- Adversarial reasoning
- Spatial intelligence
- Motor coordination
- Decision-making under uncertainty

---

# License

MIT License

---

# Author

David Ortiz

Experimental AI Robotics Research

---

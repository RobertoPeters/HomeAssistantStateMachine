# Home Assistant State Machine (HASM)
State Machine for Home Assistant and MQTT supported controllers (like Homey) to create automation using state machines, flows or scripting

<img width="1054" height="698" alt="image" src="https://github.com/user-attachments/assets/d30dc307-88a9-416f-8ae6-3f520dabe454" />

For the features and user manual see the Wiki https://github.com/RobertoPeters/HomeAssistantStateMachine/wiki


## Technical information
This is a C# Blazor web application.

The HA Client is taken from HAClient repository (due to activity on that project, it seamed safer to just include it in this project)

MQTTnet for MQTT protocol

Radzen.Blazor is used for UI

Z.Blazor.Diagrams for state machine modulation

Jint is used for scripts in states and transitiions (javascript for user)

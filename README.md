# Home Assistant State Machine (HASM)
State Machine for Home Assistant to create automation using state machines

![image](https://github.com/RobertoPeters/HomeAssistantStateMachine/assets/5654611/d25de45b-157d-4359-9b14-4b92b403b10d)

![image](https://github.com/RobertoPeters/HomeAssistantStateMachine/assets/5654611/0b24c8c9-9677-4c9e-a6c8-d880de1dfcf2)


For the features and user manual see the Wiki https://github.com/RobertoPeters/HomeAssistantStateMachine/wiki


## Technical information
This is a C# Blazor web application.

The HA Client is taken from HAClient repository (due to activity on that project, it seamed safer to just include it in this project)

Radzen.Blazor is used for UI

Z.Blazor.Diagrams for state machine modulation

Entity Framework with SQlite is used for storage and ORM

Jint is used for scripts in states and transitiions (javascript for user)

# Home Assistant State Machine (HASM)
State Machine for Home Assistant to create automation using state machines

This project is almost going into alpha phase
First I want to write some manual in the wiki for users to learn the application.

Some info:
## Features
User can add HA clients (to connect to HA)

User can add variables which reflects entities in HA (e.g. button1 -> input_boolean.button1)

User can create state machines like in Visio

User can create service calls to HA

A state has a entry script/code

A condition has condition script/code

...and many more...


## Technical information
This is a C# Blazor web application.

The HA Client is taken from HAClient repository (due to activity on that project, it seamed safer to just include it in this project)

Radzen.Blazor is used for UI

Z.Blazor.Diagrams for state machine modulation

Entity Framework with SQlite is used for storage and ORM

Jint is used for scripts in states and transitiions (javascript for user)

# Installation
Docker repository: robertpeters/homeassistantstatemachine:dev
https://hub.docker.com/repository/docker/robertpeters/homeassistantstatemachine/general

Internal port is 80 (http://)

volume mapping is /app/Settings


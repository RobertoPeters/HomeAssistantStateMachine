﻿// <auto-generated />
using System;
using HomeAssistantStateMachine.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace HomeAssistantStateMachine.Migrations
{
    [DbContext(typeof(HasmDbContext))]
    [Migration("20240428114016_addingMqtt")]
    partial class addingMqtt
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.4");

            modelBuilder.Entity("HomeAssistantStateMachine.Models.HAClient", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Enabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Host")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("HAClients");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.MqttClient", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Enabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Host")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<string>("Password")
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<bool>("Tls")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Username")
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<bool>("WebSocket")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("MqttClients");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.State", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.Property<string>("EntryAction")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsErrorState")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsStartState")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<int?>("StateMachineId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UIData")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("StateMachineId");

                    b.ToTable("States");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.StateMachine", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Enabled")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<string>("PreScheduleAction")
                        .HasColumnType("TEXT");

                    b.Property<string>("PreStartAction")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("StateMachines");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.Transition", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Condition")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.Property<int?>("FromStateId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("StateMachineId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("ToStateId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UIData")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("FromStateId");

                    b.HasIndex("StateMachineId");

                    b.HasIndex("ToStateId");

                    b.ToTable("Transitions");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.Variable", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Data")
                        .HasColumnType("TEXT");

                    b.Property<int?>("HAClientId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("MqttClientId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("TEXT");

                    b.Property<int?>("StateId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("StateMachineId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("HAClientId");

                    b.HasIndex("MqttClientId");

                    b.HasIndex("StateId");

                    b.HasIndex("StateMachineId");

                    b.ToTable("Variables");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.VariableValue", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("Update")
                        .HasColumnType("TEXT");

                    b.Property<string>("Value")
                        .HasColumnType("TEXT");

                    b.Property<int>("VariableId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("VariableId");

                    b.ToTable("VariableValues");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.State", b =>
                {
                    b.HasOne("HomeAssistantStateMachine.Models.StateMachine", "StateMachine")
                        .WithMany("States")
                        .HasForeignKey("StateMachineId");

                    b.Navigation("StateMachine");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.Transition", b =>
                {
                    b.HasOne("HomeAssistantStateMachine.Models.State", "FromState")
                        .WithMany()
                        .HasForeignKey("FromStateId");

                    b.HasOne("HomeAssistantStateMachine.Models.StateMachine", "StateMachine")
                        .WithMany("Transitions")
                        .HasForeignKey("StateMachineId");

                    b.HasOne("HomeAssistantStateMachine.Models.State", "ToState")
                        .WithMany()
                        .HasForeignKey("ToStateId");

                    b.Navigation("FromState");

                    b.Navigation("StateMachine");

                    b.Navigation("ToState");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.Variable", b =>
                {
                    b.HasOne("HomeAssistantStateMachine.Models.HAClient", "HAClient")
                        .WithMany()
                        .HasForeignKey("HAClientId");

                    b.HasOne("HomeAssistantStateMachine.Models.MqttClient", "MqttClient")
                        .WithMany()
                        .HasForeignKey("MqttClientId");

                    b.HasOne("HomeAssistantStateMachine.Models.State", "State")
                        .WithMany()
                        .HasForeignKey("StateId");

                    b.HasOne("HomeAssistantStateMachine.Models.StateMachine", "StateMachine")
                        .WithMany()
                        .HasForeignKey("StateMachineId");

                    b.Navigation("HAClient");

                    b.Navigation("MqttClient");

                    b.Navigation("State");

                    b.Navigation("StateMachine");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.VariableValue", b =>
                {
                    b.HasOne("HomeAssistantStateMachine.Models.Variable", "Variable")
                        .WithMany()
                        .HasForeignKey("VariableId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Variable");
                });

            modelBuilder.Entity("HomeAssistantStateMachine.Models.StateMachine", b =>
                {
                    b.Navigation("States");

                    b.Navigation("Transitions");
                });
#pragma warning restore 612, 618
        }
    }
}
﻿using HassClient.Helpers;
using HassClient.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HassClient.Models
{
    /// <summary>
    /// Represents a single entity's state.
    /// </summary>
    public class StateModel
    {
        /// <summary>
        /// Gets the Entity ID that this state represents.
        /// </summary>
        [JsonProperty]
        public string EntityId { get; private set; }

        /// <summary>
        /// Gets the string representation of the state that this entity is currently in.
        /// </summary>
        [JsonProperty]
        public string State { get; private set; }

        /// <summary>
        /// Gets the state that this entity is currently in as a <see cref="KnownStates"/>.
        /// </summary>
        [JsonIgnore]
        public KnownStates KnownState => this.State.AsKnownState();

        /// <summary>
        /// Gets the entity's current attributes and values.
        /// </summary>
        [JsonProperty]
        public Dictionary<string, JRaw> Attributes { get; private set; }

        /// <summary>
        /// Gets the context for this entity's state.
        /// </summary>
        [JsonProperty]
        public Context Context { get; internal set; }

        /// <summary>
        /// Gets the UTC date and time that this state was last changed.
        /// </summary>
        [JsonProperty]
        public DateTimeOffset LastChanged { get; private set; }

        /// <summary>
        /// Gets the UTC date and time that this state was last updated.
        /// </summary>
        [JsonProperty]
        public DateTimeOffset LastUpdated { get; private set; }

        /// <summary>
        /// Attempts to get the value of the specified attribute by <paramref name="name"/>,
        /// and cast the value to type <typeparamref name="T" />.
        /// </summary>
        /// <exception cref="InvalidCastException">Thrown when the specified type <typeparamref name="T"/>
        /// cannot be cast to the attribute's current value.</exception>
        /// <typeparam name="T">The desired type to cast the attribute value to.</typeparam>
        /// <param name="name">The name of the attribute to retrieve the value for.</param>
        /// <returns>The attribute's current value, cast to type <typeparamref name="T" />.</returns>
        public T GetAttributeValue<T>(string name) => !this.Attributes.ContainsKey(name) ? default : HassSerializer.DeserializeObject<T>(this.Attributes[name]);

        /// <summary>
        /// Attempts to get the values of the specified attribute by <paramref name="name"/> as an
        /// <see cref="IEnumerable{T}"/>.
        /// <para>
        /// If the attribute is not defined, an empty enumeration will be returned.
        /// </para>
        /// </summary>
        /// <exception cref="InvalidCastException">Thrown when the specified type <typeparamref name="T"/>
        /// cannot be cast to the attribute's current value.</exception>
        /// <typeparam name="T">The desired type to cast the attribute values to.</typeparam>
        /// <param name="name">The name of the attribute to retrieve the values for.</param>
        /// <returns>
        /// An enumeration containing attribute's current values, cast to type <typeparamref name="T" />.
        /// </returns>
        public IEnumerable<T> GetAttributeValues<T>(string name)
        {
            return this.GetAttributeValue<IEnumerable<T>>(name) ?? Enumerable.Empty<T>();
        }

        internal TEnum GetAttributeValue<TEnum>(string name, KnownEnumCache<TEnum> knownEnumCache)
            where TEnum : struct, Enum
        {
            return knownEnumCache.AsEnum(this.GetAttributeValue<string>(name));
        }

        /// <inheritdoc />
        public override string ToString() => $"{this.EntityId}: {this.State}";
    }
}

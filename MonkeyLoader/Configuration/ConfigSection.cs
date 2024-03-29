﻿using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.Configuration
{
    /// <summary>
    /// Represents a section of a <see cref="Configuration.Config"/> for any <see cref="IConfigOwner"/>.
    /// </summary>
    /// <remarks>
    /// Use your mod's <see cref="Configuration.Config"/> instance to <see cref="Config.LoadSection{TSection}()">load sections</see>.
    /// </remarks>
    public abstract class ConfigSection
    {
        /// <summary>
        /// Stores the <see cref="IDefiningConfigKey"/>s tracked by this section.
        /// </summary>
        protected readonly HashSet<IDefiningConfigKey> keys;

        /// <summary>
        /// Gets the <see cref="Configuration.Config"/> that this section is a part of.
        /// </summary>
        // Make the Compiler shut up about Config not being set - it gets set by the Config loading the section.
        public Config Config { get; internal set; } = null!;

        /// <summary>
        /// Gets a description of the config items found in this section.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Gets whether there are any config keys with unsaved changes in this section.
        /// </summary>
        public bool HasChanges => keys.Any(key => key.HasChanges);

        /// <summary>
        /// Gets all the config keys of this section.
        /// </summary>
        public IEnumerable<IDefiningConfigKey> Keys => keys.AsSafeEnumerable();

        /// <summary>
        /// Gets the name of the section.<br/>
        /// Must be unique for a given mod.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets whether this config section is allowed to be saved.<br/>
        /// This can be <c>false</c> if something went wrong while loading it.
        /// </summary>
        public bool Saveable { get; internal set; } = true;

        /// <summary>
        /// Gets the semantic version for this config section.<br/>
        /// This is used to check if the defined and saved configs are compatible.
        /// </summary>
        public abstract Version Version { get; }

        /// <summary>
        /// Gets the way that an incompatible saved configuration should be treated.<br/>
        /// <see cref="IncompatibleConfigHandling.Error"/> by default
        /// </summary>
        protected virtual IncompatibleConfigHandling IncompatibilityHandling => IncompatibleConfigHandling.Error;

        /// <summary>
        /// Creates a new config section instance.
        /// </summary>
        protected ConfigSection()
        {
            keys = new(GetConfigKeys());

            foreach (var key in keys)
                key.Section = this;
        }

        /// <summary>
        /// Checks if two <see cref="ConfigSection"/>s are unequal.
        /// </summary>
        /// <param name="left">The first section.</param>
        /// <param name="right">The second section.</param>
        /// <returns><c>true</c> if they're considered unequal.</returns>
        public static bool operator !=(ConfigSection? left, ConfigSection? right)
            => !(left == right);

        /// <summary>
        /// Checks if two <see cref="ConfigSection"/>s are equal.
        /// </summary>
        /// <param name="left">The first section.</param>
        /// <param name="right">The second section.</param>
        /// <returns><c>true</c> if they're considered equal.</returns>
        public static bool operator ==(ConfigSection? left, ConfigSection? right)
            => ReferenceEquals(left, right)
            || (left is not null && right is not null && left.Name == right.Name);

        /// <summary>
        /// Checks if the given object can be considered equal to this one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns><c>true</c> if the other object is considered equal.</returns>
        public override bool Equals(object obj) => obj is ConfigSection section && section == this;

        /// <inheritdoc/>
        public override int GetHashCode() => Name.GetHashCode();

        internal void Load(JObject source, JsonSerializer jsonSerializer)
        {
            Version serializedVersion;

            try
            {
                serializedVersion = new Version((string)source[nameof(Version)]!);
            }
            catch (Exception ex)
            {
                // I know not what exceptions the JSON library will throw, but they must be contained
                Saveable = false;
                throw new ConfigLoadException($"Error loading version for section [{Name}]!", ex);
            }

            ValidateCompatibility(serializedVersion);

            OnLoad(source, jsonSerializer);
        }

        internal void ResetHasChanges()
        {
            foreach (var key in keys)
                key.HasChanges = false;
        }

        internal JObject? Save(JsonSerializer jsonSerializer)
        {
            if (!Saveable)
                return null;

            var result = new JObject();
            result["Version"] = Version.ToString();

            // Any exceptions get handled by the Config.Save method
            OnSave(result, jsonSerializer);

            return result;
        }

        /// <summary>
        /// Gets the <see cref="IDefiningConfigKey"/>s from all fields of this <see cref="ConfigSection"/> which have a <see cref="Type"/>
        /// derived from <see cref="IDefiningConfigKey"/> and don't have a <see cref="IgnoreConfigKeyAttribute"/>.
        /// </summary>
        /// <returns>The automatically tracked <see cref="IDefiningConfigKey"/>s.</returns>
        protected IEnumerable<IDefiningConfigKey> GetAutoConfigKeys()
        {
            var configKeyType = typeof(IDefiningConfigKey);

            return GetType().GetFields(AccessTools.all | BindingFlags.FlattenHierarchy)
                .Where(field => configKeyType.IsAssignableFrom(field.FieldType)
                             && field.GetCustomAttribute<IgnoreConfigKeyAttribute>() is null)
                .Select(field => field.GetValue(this))
                .Cast<IDefiningConfigKey>();
        }

        /// <summary>
        /// Gets all <see cref="IDefiningConfigKey"/>s which should be tracked for this <see cref="ConfigSection"/>.
        /// </summary>
        /// <remarks>
        /// <i>By default</i>: Calls <see cref="GetAutoConfigKeys"/>.
        /// </remarks>
        /// <returns>All <see cref="IDefiningConfigKey"/>s to track.</returns>
        protected virtual IEnumerable<IDefiningConfigKey> GetConfigKeys() => GetAutoConfigKeys();

        /// <summary>
        /// Deserializes all <see cref="Keys">keys</see> of this
        /// <see cref="ConfigSection"/> from the <paramref name="source"/> <see cref="JObject"/>.
        /// </summary>
        /// <remarks>
        /// <i>By default</i>: Deserializes all <see cref="Keys">keys</see> from the <paramref name="source"/> with the <paramref name="jsonSerializer"/>.
        /// </remarks>
        /// <param name="source">The <see cref="JObject"/> being deserialized from.</param>
        /// <param name="jsonSerializer">The <see cref="JsonSerializer"/> to deserialize objects with.</param>
        /// <exception cref="ConfigLoadException">When the value for a key fails to deserialize.</exception>
        protected virtual void OnLoad(JObject source, JsonSerializer jsonSerializer)
        {
            foreach (var key in keys)
            {
                try
                {
                    if (source[key.Name] is JToken token)
                    {
                        var value = token.ToObject(key.ValueType, jsonSerializer);
                        key.SetValue(value, "Load");
                        key.HasChanges = false;
                    }
                }
                catch (Exception ex)
                {
                    // I know not what exceptions the JSON library will throw, but they must be contained
                    Saveable = false;
                    throw new ConfigLoadException($"Error loading key [{key.Name}] of type [{key.ValueType}] in section [{Name}]!", ex);
                }
            }
        }

        /// <summary>
        /// Serializes all <see cref="Keys">keys</see> of this
        /// <see cref="ConfigSection"/> to the <paramref name="result"/> <see cref="JObject"/>.
        /// </summary>
        /// <remarks>
        /// <i>By default</i>: Serializes all <see cref="Keys">keys</see> to the <paramref name="result"/> with the <paramref name="jsonSerializer"/>.
        /// </remarks>
        /// <param name="result">The <see cref="JObject"/> being serialized to.</param>
        /// <param name="jsonSerializer">The <see cref="JsonSerializer"/> to serialize objects with.</param>
        protected virtual void OnSave(JObject result, JsonSerializer jsonSerializer)
        {
            foreach (var key in keys)
            {
                if (!key.TryGetValue(out var value))
                    continue;

                // I don't need to typecheck this as there's no way to sneak a bad type past my Set() API
                result[key.Name] = value == null ? null : JToken.FromObject(value, jsonSerializer);
            }
        }

        private static bool AreVersionsCompatible(Version serializedVersion, Version currentVersion)
        {
            if (serializedVersion.Major != currentVersion.Major)
            {
                // major version differences are hard incompatible
                return false;
            }

            if (serializedVersion.Minor > currentVersion.Minor)
            {
                // if serialized config has a newer minor version than us
                // in other words, someone downgraded the mod but not the config
                // then we cannot load the config
                return false;
            }

            // none of the checks failed!
            return true;
        }

        private void ValidateCompatibility(Version serializedVersion)
        {
            if (!AreVersionsCompatible(serializedVersion, Version))
            {
                switch (IncompatibilityHandling)
                {
                    case IncompatibleConfigHandling.Clobber:
                        Config.Logger.Warn(() => $"Saved section [{Name}] version [{serializedVersion}] is incompatible with mod's version [{Version}]. Clobbering old config and starting fresh.");
                        return;

                    case IncompatibleConfigHandling.ForceLoad:
                        // continue processing
                        break;

                    case IncompatibleConfigHandling.Error: // fall through to default
                    default:
                        Saveable = false;
                        throw new ConfigLoadException($"Saved section [{Name}] version [{serializedVersion}] is incompatible with mod's version [{Version}]!");
                }
            }
        }
    }
}
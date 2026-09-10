using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;

namespace ConfigurationManager
{
    // Upstream also recognizes shudnal/ConditionalConfigSync-backed configs
    // here; dropped in this vendored copy since nothing in this modpack uses
    // it (see modpack.yaml) -- not worth a fourth external project just for
    // an optional lock-icon tooltip. Jotunn/ServerSync detection (below)
    // is untouched and still recognizes any ServerSync-based config by type
    // name via reflection, including our own vendored ConfigSync.cs.
    internal enum ConfigSynchronizationProvider
    {
        None,
        Jotunn,
        ServerSync,
    }

    internal readonly struct ConfigSynchronizationState
    {
        internal static readonly ConfigSynchronizationState None = new ConfigSynchronizationState(
            ConfigSynchronizationProvider.None,
            isServerControlled: false,
            isConditional: false,
            isOverridden: false,
            canChangePolicy: false,
            tooltip: string.Empty);

        internal ConfigSynchronizationState(
            ConfigSynchronizationProvider provider,
            bool isServerControlled,
            bool isConditional,
            bool isOverridden,
            bool canChangePolicy,
            string tooltip)
        {
            Provider = provider;
            IsServerControlled = isServerControlled;
            IsConditional = isConditional;
            IsOverridden = isOverridden;
            CanChangePolicy = canChangePolicy;
            Tooltip = tooltip;
        }

        internal ConfigSynchronizationProvider Provider { get; }
        internal bool IsServerControlled { get; }
        internal bool IsConditional { get; }
        internal bool IsOverridden { get; }
        internal bool CanChangePolicy { get; }
        internal string Tooltip { get; }
        internal bool IsVisible => IsConditional || IsServerControlled || IsOverridden;
    }

    internal abstract class ConfigSynchronizationInfo
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static readonly ConfigSynchronizationInfo None = new NoSynchronizationInfo();

        internal abstract ConfigSynchronizationState GetState();

        internal virtual bool TogglePolicy() => false;

        internal static ConfigSynchronizationInfo Create(ConfigEntryBase entry)
        {
            object[] tags = entry.Description?.Tags;
            if (tags == null || tags.Length == 0)
                return None;

            foreach (object tag in tags)
            {
                if (tag != null && TryCreateServerSyncInfo(entry, tag, out ConfigSynchronizationInfo serverSyncInfo))
                    return serverSyncInfo;
            }

            foreach (object tag in tags)
            {
                if (tag != null && TryCreateJotunnInfo(tag, out ConfigSynchronizationInfo jotunnInfo))
                    return jotunnInfo;
            }

            return None;
        }

        private static bool TryCreateServerSyncInfo(ConfigEntryBase entry, object tag, out ConfigSynchronizationInfo info)
        {
            info = null;
            Type type = tag.GetType();
            if (!HasBaseType(type, "ServerSync.OwnConfigEntryBase"))
                return false;

            PropertyInfo baseConfigProperty = type.GetProperty("BaseConfig", InstanceMembers);
            FieldInfo synchronizedConfigField = type.GetField("SynchronizedConfig", InstanceMembers);
            if (baseConfigProperty == null || synchronizedConfigField?.FieldType != typeof(bool))
                return false;

            try
            {
                if (!ReferenceEquals(baseConfigProperty.GetValue(tag), entry))
                    return false;
            }
            catch
            {
                return false;
            }

            info = new ReflectedBooleanSynchronizationInfo(
                ConfigSynchronizationProvider.ServerSync,
                tag,
                synchronizedConfigField,
                "ServerSync");
            return true;
        }

        private static bool TryCreateJotunnInfo(object tag, out ConfigSynchronizationInfo info)
        {
            info = null;
            Type type = tag.GetType();
            if (!string.Equals(type.Assembly.GetName().Name, "Jotunn", StringComparison.Ordinal)
                || !string.Equals(type.Name, "ConfigurationManagerAttributes", StringComparison.Ordinal))
            {
                return false;
            }

            PropertyInfo isAdminOnlyProperty = type.GetProperty("IsAdminOnly", InstanceMembers);
            if (isAdminOnlyProperty?.PropertyType != typeof(bool))
                return false;

            info = new ReflectedBooleanSynchronizationInfo(
                ConfigSynchronizationProvider.Jotunn,
                tag,
                isAdminOnlyProperty,
                "Jotunn");
            return true;
        }

        private static bool HasBaseType(Type type, string fullName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (string.Equals(current.FullName, fullName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private sealed class NoSynchronizationInfo : ConfigSynchronizationInfo
        {
            internal override ConfigSynchronizationState GetState() => ConfigSynchronizationState.None;
        }

        private sealed class ReflectedBooleanSynchronizationInfo : ConfigSynchronizationInfo
        {
            private readonly object source;
            private readonly FieldInfo field;
            private readonly PropertyInfo property;
            private readonly ConfigSynchronizationState serverControlledState;

            internal ReflectedBooleanSynchronizationInfo(
                ConfigSynchronizationProvider provider,
                object source,
                FieldInfo field,
                string providerName)
            {
                this.source = source;
                this.field = field;
                serverControlledState = CreateServerControlledState(provider, providerName);
            }

            internal ReflectedBooleanSynchronizationInfo(
                ConfigSynchronizationProvider provider,
                object source,
                PropertyInfo property,
                string providerName)
            {
                this.source = source;
                this.property = property;
                serverControlledState = CreateServerControlledState(provider, providerName);
            }

            internal override ConfigSynchronizationState GetState()
            {
                bool isServerControlled;
                try
                {
                    isServerControlled = field != null
                        ? (bool)field.GetValue(source)
                        : (bool)property.GetValue(source);
                }
                catch
                {
                    return ConfigSynchronizationState.None;
                }

                if (!isServerControlled)
                    return ConfigSynchronizationState.None;

                return serverControlledState;
            }

            private static ConfigSynchronizationState CreateServerControlledState(
                ConfigSynchronizationProvider provider,
                string providerName)
            {
                return new ConfigSynchronizationState(
                    provider,
                    isServerControlled: true,
                    isConditional: false,
                    isOverridden: false,
                    canChangePolicy: false,
                    tooltip: $"Server-controlled setting\nSynchronization provider: {providerName}\nPolicy control: Fixed by the synchronization provider");
            }
        }

        private static string FormatOwnership(bool isServerControlled)
        {
            return isServerControlled ? "Server-controlled" : "Client-controlled";
        }
    }
}

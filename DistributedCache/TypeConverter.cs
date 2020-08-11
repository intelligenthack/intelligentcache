using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace IntelligentHack.DistributedCache
{
    /// <summary>
    /// Performs type conversions using every standard provided by the .NET library.
    /// </summary>
    /// <remarks>
    /// Obtained from https://github.com/aaubry/PublicDomain/blob/master/TypeConverter.cs
    /// </remarks>
    public static class TypeConverter
    {
        /// <summary>
        /// Converts the specified value.
        /// </summary>
        /// <typeparam name="T">The type to which the value is to be converted.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns></returns>
        public static T ChangeType<T>(object? value)
        {
            return (T)ChangeType(value, typeof(T))!;
        }

        /// <summary>
        /// Converts the specified value.
        /// </summary>
        /// <typeparam name="T">The type to which the value is to be converted.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <param name="provider">The provider.</param>
        /// <returns></returns>
        public static T ChangeType<T>(object? value, IFormatProvider provider)
        {
            return (T)ChangeType(value, typeof(T), provider)!;
        }

        /// <summary>
        /// Converts the specified value.
        /// </summary>
        /// <typeparam name="T">The type to which the value is to be converted.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <param name="culture">The culture.</param>
        /// <returns></returns>
        public static T ChangeType<T>(object? value, CultureInfo culture)
        {
            return (T)ChangeType(value, typeof(T), culture)!;
        }

        /// <summary>
        /// Converts the specified value using the invariant culture.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="destinationType">The type to which the value is to be converted.</param>
        /// <returns></returns>
        public static object? ChangeType(object? value, Type destinationType)
        {
            return ChangeType(value, destinationType, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts the specified value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="destinationType">The type to which the value is to be converted.</param>
        /// <param name="provider">The format provider.</param>
        /// <returns></returns>
        public static object? ChangeType(object? value, Type destinationType, IFormatProvider provider)
        {
            return ChangeType(value, destinationType, new CultureInfoAdapter(CultureInfo.CurrentCulture, provider));
        }

        /// <summary>
        /// Converts the specified value.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="destinationType">The type to which the value is to be converted.</param>
        /// <param name="culture">The culture.</param>
        /// <returns></returns>
        public static object? ChangeType(object? value, Type destinationType, CultureInfo culture)
        {
            // Handle null and DBNull
            if (value == null || value is DBNull)
            {
                return destinationType.IsValueType ? Activator.CreateInstance(destinationType) : null;
            }

            var sourceType = value.GetType();

            // If the source type is compatible with the destination type, no conversion is needed
            if (destinationType.IsAssignableFrom(sourceType))
            {
                return value;
            }

            // Nullable types get a special treatment
            if (destinationType.IsGenericType)
            {
                var genericTypeDefinition = destinationType.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    var innerType = destinationType.GetGenericArguments()[0];
                    var convertedValue = ChangeType(value, innerType, culture);
                    return Activator.CreateInstance(destinationType, convertedValue);
                }
            }

            // Enums also require special handling
            if (destinationType.IsEnum)
            {
                var valueText = value as string;
                return valueText != null ? Enum.Parse(destinationType, valueText, true) : value;
            }

            // Special case for booleans to support parsing "1" and "0". This is
            // necessary for compatibility with XML Schema.
            if (destinationType == typeof(bool))
            {
                if ("0".Equals(value))
                    return false;

                if ("1".Equals(value))
                    return true;
            }

            // Try with the source type's converter
            var sourceConverter = TypeDescriptor.GetConverter(value);
            if (sourceConverter != null && sourceConverter.CanConvertTo(destinationType))
            {
                return sourceConverter.ConvertTo(null, culture, value, destinationType);
            }

            // Try with the destination type's converter
            var destinationConverter = TypeDescriptor.GetConverter(destinationType);
            if (destinationConverter != null && destinationConverter.CanConvertFrom(sourceType))
            {
                return destinationConverter.ConvertFrom(null, culture, value);
            }

            // Try to find a casting operator in the source or destination type
            foreach (var type in new[] { sourceType, destinationType })
            {
                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    var isCastingOperator =
                        method.IsSpecialName &&
                        (method.Name == "op_Implicit" || method.Name == "op_Explicit") &&
                        destinationType.IsAssignableFrom(method.ReturnParameter.ParameterType);

                    if (isCastingOperator)
                    {
                        var parameters = method.GetParameters();

                        var isCompatible =
                            parameters.Length == 1 &&
                            parameters[0].ParameterType.IsAssignableFrom(sourceType);

                        if (isCompatible)
                        {
                            try
                            {
                                return method.Invoke(null, new[] { value });
                            }
                            catch (TargetInvocationException ex)
                            {
                                throw UnwrapException(ex);
                            }
                        }
                    }
                }
            }

            // If source type is string, try to find a Parse or TryParse method
            if (sourceType == typeof(string))
            {
                try
                {
                    // Try with - public static T Parse(string, IFormatProvider)
                    var parseMethod = destinationType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(IFormatProvider) }, null);
                    if (parseMethod != null)
                    {
                        return parseMethod.Invoke(null, new object[] { value, culture });
                    }

                    // Try with - public static T Parse(string)
                    parseMethod = destinationType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                    if (parseMethod != null)
                    {
                        return parseMethod.Invoke(null, new object[] { value });
                    }
                }
                catch (TargetInvocationException ex)
                {
                    throw UnwrapException(ex);
                }
            }

            // Handle TimeSpan
            if (destinationType == typeof(TimeSpan))
            {
                return TimeSpan.Parse((string)ChangeType(value, typeof(string), CultureInfo.InvariantCulture)!);
            }

            // Default to the Convert class
            return Convert.ChangeType(value, destinationType, CultureInfo.InvariantCulture);
        }

        private static readonly FieldInfo remoteStackTraceField = typeof(Exception)
            .GetField("_remoteStackTraceString", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static Exception UnwrapException(TargetInvocationException ex)
        {
            var result = ex.InnerException;
            if (remoteStackTraceField != null)
            {
                remoteStackTraceField.SetValue(ex.InnerException, ex.InnerException!.StackTrace + "\r\n");
            }
            return result!;
        }

        private sealed class CultureInfoAdapter : CultureInfo
        {
            private readonly IFormatProvider _provider;

            public CultureInfoAdapter(CultureInfo baseCulture, IFormatProvider provider)
                : base(baseCulture.Name)
            {
                _provider = provider;
            }

            public override object? GetFormat(Type? formatType)
            {
                return _provider.GetFormat(formatType);
            }
        }
    }
}

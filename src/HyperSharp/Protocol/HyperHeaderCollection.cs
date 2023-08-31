using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace HyperSharp.Protocol
{
    /// <summary>
    /// Represents a collection of headers with string keys and lists of string values.
    /// </summary>
    public sealed class HyperHeaderCollection : IReadOnlyDictionary<string, IReadOnlyList<string>>
    {
        // https://developers.cloudflare.com/rules/transform/request-header-modification/reference/header-format
        private static readonly char[] _validHeaderNameCharacters = new char[]
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '_'
        };

        private static readonly char[] _validHeaderValueCharacters = new char[]
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '_',
            ' ', ':', ';', '.', ',', '\\', '/', '"', '\'', '!', '?', '(', ')', '{', '}', '[', ']', '@', '<', '>', '=', '+', '*', '#', '$', '&', '`', '|', '~', '^', '%'
        };

        private readonly Dictionary<string, List<byte[]>> _headers = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public int Count => _headers.Count;

        /// <inheritdoc/>
        public IEnumerable<string> Keys => _headers.Keys;

        /// <inheritdoc/>
        public IEnumerable<IReadOnlyList<string>> Values
        {
            get
            {
                foreach (List<byte[]> values in _headers.Values)
                {
                    yield return values.Select(Encoding.ASCII.GetString).ToList();
                }
            }
        }

        /// <inheritdoc/>
        public bool ContainsKey(string key) => _headers.ContainsKey(key);

        /// <inheritdoc/>
        public IReadOnlyList<string> this[string key] => _headers[key].Select(Encoding.ASCII.GetString).ToList();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, IReadOnlyList<string>>> GetEnumerator()
        {
            foreach (KeyValuePair<string, List<byte[]>> header in _headers)
            {
                yield return new KeyValuePair<string, IReadOnlyList<string>>(header.Key, header.Value.Select(Encoding.ASCII.GetString).ToList());
            }
        }

        /// <summary>
        /// Adds a header with a single value.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The value of the header.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="value"/> contains invalid characters.</exception>
        public void Add(string name, string value)
        {
            ValidateArgumentParameters(name, value);
            if (_headers.TryGetValue(name, out List<byte[]>? values))
            {
                values.Add(Encoding.ASCII.GetBytes(value));
            }
            else
            {
                _headers.Add(name, new List<byte[]> { Encoding.ASCII.GetBytes(value) });
            }
        }


        /// <summary>
        /// Adds a header with multiple values.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="values">The values of the header.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="values"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="values"/> contains invalid characters.</exception>
        public void Add(string name, IEnumerable<string> values)
        {
            ValidateArgumentParameters(name, values);
            if (!_headers.TryGetValue(name, out List<byte[]>? existingValues))
            {
                existingValues = new List<byte[]>();
                _headers.Add(name, existingValues);
            }

            foreach (string value in values)
            {
                existingValues.Add(Encoding.ASCII.GetBytes(value));
            }
        }

        /// <summary>
        /// Adds a header with a single value in ASCII byte form.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The value of the header, in 7-bit ASCII form.</param>
        public void Add(string name, ReadOnlySpan<byte> value)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            else if (!IsValidName(name))
            {
                throw new ArgumentException($"Invalid header name: {name}", nameof(name));
            }

            ReadOnlySpan<char> validCharSpan = _validHeaderValueCharacters.AsSpan();
            byte[] characters = new byte[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                char asciiCharacter = (char)(value[i] & 255);
                if (!validCharSpan.Contains(asciiCharacter))
                {
                    throw new ArgumentException($"Invalid header value: {characters}", nameof(value));
                }

                // Trim leading whitespace
                if (char.IsWhiteSpace(asciiCharacter) && i == 0)
                {
                    continue;
                }

                characters[i] = (byte)asciiCharacter;
            }

            if (_headers.TryGetValue(name, out List<byte[]>? values))
            {
                values.Add(characters);
            }
            else
            {
                _headers.Add(name, new List<byte[]> { characters });
            }
        }

        /// <summary>
        /// Adds a header with a single value if the header does not already exist.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The value of the header.</param>
        /// <returns><see langword="true"/> if the parameters are valid and added to the collection; otherwise, <see langword="false"/>.</returns>
        public bool TryAdd(string name, string value)
        {
            if (name is null || !IsValidName(name) || _headers.ContainsKey(name) || value is null || !IsValidValue(value))
            {
                return false;
            }

            _headers.Add(name, new List<byte[]> { Encoding.ASCII.GetBytes(value) });
            return true;
        }

        /// <summary>
        /// Adds a header with multiple values if the header does not already exist.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="values">The values of the header.</param>
        /// <returns><see langword="true"/> if the parameters are valid and added to the collection; otherwise, <see langword="false"/>.</returns>
        public bool TryAdd(string name, IEnumerable<string> values)
        {
            if (name is null || !IsValidName(name) || values is null || _headers.ContainsKey(name))
            {
                return false;
            }

            List<byte[]> newValues = new();
            foreach (string value in values)
            {
                if (!IsValidValue(value))
                {
                    return false;
                }

                newValues.Add(Encoding.ASCII.GetBytes(value));
            }

            _headers.Add(name, newValues);
            return true;
        }

        /// <summary>
        /// Adds a header with a single value in ASCII byte form if the header does not already exist.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The value of the header, in 7-bit ASCII form.</param>
        /// <returns><see langword="true"/> if the parameters are valid and added to the collection; otherwise, <see langword="false"/>.</returns>
        public bool TryAdd(string name, ReadOnlySpan<byte> value)
        {
            if (name is null || !IsValidName(name) || _headers.ContainsKey(name))
            {
                return false;
            }

            ReadOnlySpan<char> validCharSpan = _validHeaderValueCharacters.AsSpan();
            byte[] characters = new byte[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                char asciiCharacter = (char)(value[i] & 255);
                if (!validCharSpan.Contains(asciiCharacter))
                {
                    return false;
                }

                // Trim leading whitespace
                if (char.IsWhiteSpace(asciiCharacter) && i == 0)
                {
                    continue;
                }

                characters[i] = (byte)asciiCharacter;
            }

            _headers.Add(name, new List<byte[]> { characters });
            return true;
        }

        /// <summary>
        /// Sets a header with a single value.  If the header already exists, it will be overwritten.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The value of the header.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="value"/> contains invalid characters.</exception>
        public void Set(string name, string value)
        {
            ValidateArgumentParameters(name, value);
            if (!_headers.TryGetValue(name, out List<byte[]>? values))
            {
                _headers.Add(name, new List<byte[]> { Encoding.ASCII.GetBytes(value) });
                return;
            }

            values.Clear();
            values.Add(Encoding.ASCII.GetBytes(value));
        }

        /// <summary>
        /// Sets a header with multiple values.  If the header already exists, it will be overwritten.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="values">The values of the header.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="values"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="values"/> contains invalid characters.</exception>
        public void Set(string name, IEnumerable<string> values)
        {
            ValidateArgumentParameters(name, values);
            if (!_headers.TryGetValue(name, out List<byte[]>? existingValues))
            {
                existingValues = new List<byte[]>();
                _headers.Add(name, values.Select(Encoding.ASCII.GetBytes).ToList());
                return;
            }

            existingValues.Clear();
            foreach (string value in values)
            {
                existingValues.Add(Encoding.ASCII.GetBytes(value));
            }
        }

        /// <summary>
        /// Removes the header with the specified name and all of its values. <paramref name="name"/> will be normalized from x-Header-name to X-Header-Name format before being used.
        /// </summary>
        /// <param name="name">The name of the header to remove.</param>
        /// <returns><see langword="true"/> if the header was found and removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(string name)
        {
            ValidateArgumentParameters(name, string.Empty);
            return _headers.Remove(name);
        }

        /// <summary>
        /// Searches for the header with the specified name and returns the first value. <paramref name="name"/> will be normalized from x-Header-name to X-Header-Name format before being used.
        /// </summary>
        /// <param name="name">The name of the header to search for.</param>
        /// <param name="value">The first value of the header if found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the header was found; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue(string name, [MaybeNullWhen(false)] out string value)
        {
            ValidateArgumentParameters(name, string.Empty);
            if (!_headers.TryGetValue(name, out List<byte[]>? existingValues))
            {
                value = null;
                return false;
            }

            value = Encoding.ASCII.GetString(existingValues[0]);
            return true;
        }

        /// <summary>
        /// Searches for the header with the specified name and returns all of its values. <paramref name="name"/> will be normalized from x-Header-name to X-Header-Name format before being used.
        /// </summary>
        /// <param name="name">The name of the header to search for.</param>
        /// <param name="values">All of the values of the header if found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the header was found; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue(string name, [MaybeNullWhen(false)] out IReadOnlyList<string> values)
        {
            ValidateArgumentParameters(name, string.Empty);
            if (!_headers.TryGetValue(name, out List<byte[]>? existingValues))
            {
                values = null;
                return false;
            }

            values = existingValues.Select(Encoding.ASCII.GetString).ToList();
            return true;
        }

        /// <summary>
        /// Searches for the header with the specified name and returns the first value in ASCII byte form. <paramref name="name"/> will be normalized from x-Header-name to X-Header-Name format before being used.
        /// </summary>
        /// <param name="name">The name of the header to search for.</param>
        /// <param name="value">The first value of the header if found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the header was found; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue(string name, [MaybeNullWhen(false)] out byte[] value)
        {
            ValidateArgumentParameters(name, string.Empty);
            if (!_headers.TryGetValue(name, out List<byte[]>? existingValues))
            {
                value = null;
                return false;
            }

            value = existingValues[0];
            return true;
        }

        /// <summary>
        /// Searches for the header with the specified name and returns all of its values in ASCII byte form. <paramref name="name"/> will be normalized from x-Header-name to X-Header-Name format before being used.
        /// </summary>
        /// <param name="name">The name of the header to search for.</param>
        /// <param name="values">All of the values of the header if found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the header was found; otherwise, <see langword="false"/>.</returns>
        public bool TryGetValue(string name, [MaybeNullWhen(false)] out IReadOnlyList<byte[]> values)
        {
            ValidateArgumentParameters(name, string.Empty);
            if (!_headers.TryGetValue(name, out List<byte[]>? existingValues))
            {
                values = null;
                return false;
            }

            values = existingValues;
            return true;
        }

        /// <summary>
        /// Validates and normalizes the header name and value parameters, throwing exceptions if invalid characters were found.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="value">The value of the header.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="value"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="value"/> contains invalid characters.</exception>
        private static void ValidateArgumentParameters(string name, string value)
        {
            ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentNullException.ThrowIfNull(value, nameof(value));

            if (!IsValidName(name))
            {
                throw new ArgumentException($"Invalid header name: {name}", nameof(name));
            }
            else if (!IsValidValue(value))
            {
                throw new ArgumentException($"Invalid header value: {value}", nameof(value));
            }
        }

        /// <summary>
        /// Validates and normalizes the header name and value parameters, throwing exceptions if invalid characters were found.
        /// </summary>
        /// <param name="name">The name of the header.</param>
        /// <param name="values">The values of the header.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="values"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="values"/> contains invalid characters.</exception>
        private static void ValidateArgumentParameters(string name, IEnumerable<string> values)
        {
            ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentNullException.ThrowIfNull(values, nameof(values));

            if (!IsValidName(name))
            {
                throw new ArgumentException($"Invalid header name: {name}", nameof(name));
            }

            foreach (string item in values)
            {
                if (!IsValidValue(item))
                {
                    throw new ArgumentException($"Invalid header value: {item}", nameof(values));
                }
            }
        }

        /// <summary>
        /// Validates that the header is in the format of <c>name: value</c>.
        /// </summary>
        /// <param name="header">The header to validate.</param>
        /// <returns><see langword="true"/> if the header is valid; otherwise, <see langword="false"/>.</returns>
        public static bool IsValidHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return false;
            }

            int colonIndex = header.IndexOf(':');
            return colonIndex != -1
                && IsValidName(header.AsSpan(0, colonIndex))
                && IsValidValue(header.AsSpan(colonIndex + 1));
        }

        /// <summary>
        /// Validates that the header name does not contain invalid characters.
        /// </summary>
        /// <param name="value">The header name to validate.</param>
        /// <returns><see langword="true"/> if the header name is valid; otherwise, <see langword="false"/>.</returns>
        public static bool IsValidName(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
            {
                return false;
            }

            Span<char> validNameSpan = _validHeaderNameCharacters.AsSpan();
            foreach (char character in value)
            {
                if (!validNameSpan.Contains(character))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates that the header value does not contain invalid characters.
        /// </summary>
        /// <param name="value">The header value to validate.</param>
        /// <returns><see langword="true"/> if the header value is valid; otherwise, <see langword="false"/>.</returns>
        public static bool IsValidValue(ReadOnlySpan<char> value)
        {
            Span<char> validValueSpan = _validHeaderValueCharacters.AsSpan();
            foreach (char character in value)
            {
                if (!validValueSpan.Contains(character))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Normalizes the header name from x-Header-name to X-Header-Name format.
        /// </summary>
        /// <param name="value">The header name to normalize.</param>
        /// <returns>The normalized header name.</returns>
        public static string NormalizeHeaderName(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
            {
                return string.Empty;
            }

            Span<char> characters = value.ToArray();
            characters[0] = char.ToUpperInvariant(value[0]);
            for (int i = 1; i < value.Length; i++)
            {
                if (value[i] is '-' or '_')
                {
                    characters[i + 1] = char.ToUpperInvariant(value[i + 1]);
                }
            }

            return characters.ToString();
        }
    }
}

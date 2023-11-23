﻿using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace LayeredTemplate.Infrastructure.Utilities;

internal static class Rfc6238Service
{
    private static readonly TimeSpan TimeStep = TimeSpan.FromMinutes(3);
    private static readonly Encoding Encoding = new UTF8Encoding(false, true);

    // Generates a new 80-bit security token
    public static byte[] GenerateRandomKey()
    {
        var bytes = new byte[20];

        RandomNumberGenerator.Fill(bytes);

        return bytes;
    }

    public static int GenerateCode(byte[] securityToken, string? modifier = null)
    {
        if (securityToken == null)
        {
            throw new ArgumentNullException(nameof(securityToken));
        }

        // Allow a variance of no greater than 9 minutes in either direction
        var currentTimeStep = GetCurrentTimeStepNumber();
        using var hashAlgorithm = new HMACSHA1(securityToken);
        return ComputeTotp(hashAlgorithm, currentTimeStep, modifier);
    }

    public static bool ValidateCode(byte[] securityToken, int code, string? modifier = null)
    {
        if (securityToken == null)
        {
            throw new ArgumentNullException(nameof(securityToken));
        }

        // Allow a variance of no greater than 9 minutes in either direction
        var currentTimeStep = GetCurrentTimeStepNumber();
        using var hashAlgorithm = new HMACSHA1(securityToken);
        for (var i = -2; i <= 2; i++)
        {
            var computedTotp = ComputeTotp(hashAlgorithm, (ulong)((long)currentTimeStep + i), modifier);
            if (computedTotp == code)
            {
                return true;
            }
        }

        // No match
        return false;
    }

    internal static int ComputeTotp(HashAlgorithm hashAlgorithm, ulong timestepNumber, string? modifier)
    {
        // # of 0's = length of pin
        const int mod = 1000000;

        // See https://tools.ietf.org/html/rfc4226
        // We can add an optional modifier
        var timestepAsBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((long)timestepNumber));
        var hash = hashAlgorithm.ComputeHash(ApplyModifier(timestepAsBytes, modifier));

        // Generate DT string
        var offset = hash[^1] & 0xf;
        Debug.Assert(offset + 4 < hash.Length, "offset + 4 < hash.Length");
        var binaryCode = (hash[offset] & 0x7f) << 24
                         | (hash[offset + 1] & 0xff) << 16
                         | (hash[offset + 2] & 0xff) << 8
                         | (hash[offset + 3] & 0xff);

        return binaryCode % mod;
    }

    private static byte[] ApplyModifier(byte[] input, string? modifier)
    {
        if (string.IsNullOrEmpty(modifier))
        {
            return input;
        }

        var modifierBytes = Encoding.GetBytes(modifier);
        var combined = new byte[checked(input.Length + modifierBytes.Length)];
        Buffer.BlockCopy(input, 0, combined, 0, input.Length);
        Buffer.BlockCopy(modifierBytes, 0, combined, input.Length, modifierBytes.Length);
        return combined;
    }

    // More info: https://tools.ietf.org/html/rfc6238#section-4
    private static ulong GetCurrentTimeStepNumber()
    {
        var delta = DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch;

        return (ulong)(delta.Ticks / TimeStep.Ticks);
    }
}
// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

static class RepositoryUrl
{
    public static string? FromRemote(string remote)
    {
        if (remote.StartsWith("git@github.com:", StringComparison.Ordinal))
        {
            remote = $"https://github.com/{remote[15..]}";
        }
        if (!Uri.TryCreate(remote, UriKind.Absolute, out var uri) || !IsHttp(uri))
        {
            return null;
        }

        var url = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri.AbsoluteUri;
        return url.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? url[..^4] : url;
    }

    static bool IsHttp(Uri uri) => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}

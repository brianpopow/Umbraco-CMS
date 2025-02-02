// Copyright (c) Umbraco.
// See LICENSE for more details.

using System;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Extensions
{
    public static class UmbracoContextAccessorExtensions
    {
        public static IUmbracoContext GetRequiredUmbracoContext(this IUmbracoContextAccessor umbracoContextAccessor)
        {
            if (umbracoContextAccessor == null) throw new ArgumentNullException(nameof(umbracoContextAccessor));
            if(!umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext))
            {
                throw new InvalidOperationException("Wasn't able to get an UmbracoContext");
            }
            return umbracoContext;
        }
    }
}

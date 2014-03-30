/*****************************************************
 * Breeze Labs: EdmBuilder
 *
 * v.1.0.3
 * Copyright 2014 IdeaBlade, Inc.  All Rights Reserved.  
 * Licensed under the MIT License
 * http://opensource.org/licenses/mit-license.php
 * 
 * Thanks to Javier Calvarro Nelson for the initial version
 * Thanks to Serkan Holat for the ModelFirst extension
 *****************************************************/
using Microsoft.Data.Edm.Csdl;
using Microsoft.Data.Edm.Validation;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.EntityClient;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.Data.Edm
{
    /// <summary>
    /// DbContext extension that builds an "Entity Data Model" (EDM) from a <see cref="DbContext"/>
    /// </summary>
    /// <remarks>
    /// We need the EDM both to define the Web API OData route and as a
    /// source of metadata for the Breeze client. 
    /// <p>
    /// The Web API OData literature recommends the
    /// <see cref="System.Web.Http.OData.Builder.ODataConventionModelBuilder"/>.
    /// That component is suffient for route definition but fails as a source of 
    /// metadata for Breeze because (as of this writing) it neglects to include the
    /// foreign key definitions Breeze requires to maintain navigation properties
    /// of client-side JavaScript entities.
    /// </p><p>
    /// This EDM Builder ask the EF DbContext to supply the metadata which 
    /// satisfy both route definition and Breeze.
    /// </p><p>
    /// This class can be downloaded and installed as a nuget package:
    /// http://www.nuget.org/packages/Breeze.EdmBuilder/
    /// </p>
    /// </remarks>
    public static class EdmBuilder
    {
        /// <summary>
        /// [OBSOLETE] Builds an Entity Data Model (EDM) from an existing <see cref="DbContext"/> 
        /// created using Code-First. Use <see cref="GetCodeFirstEdm"/> instead.
        /// </summary>
        /// <remarks>
        /// This method delegates directly to <see cref="GetCodeFirstEdm"/> whose
        /// name better describes its purpose and specificity.
        /// Deprecated for backward compatibility.
        /// </remarks>
        [Obsolete("This method is obsolete. Use GetCodeFirstEdm instead")]
        public static IEdmModel GetEdm<T>(this T dbContext) where T : DbContext, new()
        {
            return GetCodeFirstEdm<T>(dbContext);
        }

        /// <summary>
        /// Builds an Entity Data Model (EDM) from a <see cref="DbContext"/> created using Code-First.
        /// Use <see cref="GetModelFirstEdm"/> for a Model-First DbContext.
        /// </summary>
        /// <typeparam name="T">Type of the source <see cref="DbContext"/></typeparam>
        /// <returns>An XML <see cref="IEdmModel"/>.</returns>
        /// <example>
        /// <![CDATA[
        /// /* In the WebApiConfig.cs */
        /// config.Routes.MapODataRoute(
        ///     routeName: "odata", 
        ///     routePrefix: "odata", 
        ///     model: EdmBuilder.GetCodeFirstEdm<CodeFirstDbContext>(), 
        ///     batchHandler: new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer)
        ///     );
        /// ]]>
        /// </example>
        public static IEdmModel GetCodeFirstEdm<T>() where T : DbContext, new()
        {
            return GetCodeFirstEdm(new T());
        }

        /// <summary>
        /// Extension method builds an Entity Data Model (EDM) from an
        /// existing <see cref="DbContext"/> created using Code-First.
        /// Use <see cref="GetModelFirstEdm"/> for a Model-First DbContext.
        /// </summary>
        /// <typeparam name="T">Type of the source <see cref="DbContext"/></typeparam>
        /// <param name="dbContext">Concrete <see cref="DbContext"/> to use for EDM generation.</param>
        /// <returns>An XML <see cref="IEdmModel"/>.</returns>
        /// <example>
        /// <![CDATA[
        /// /* In the WebApiConfig.cs */
        /// config.Routes.MapODataRoute(
        ///     routeName: "odata", 
        ///     routePrefix: "odata", 
        ///     model: context.GetCodeFirstEdm<>(), 
        ///     batchHandler: new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer)
        ///     );
        /// ]]>
        /// </example>
        public static IEdmModel GetCodeFirstEdm<T>(this T dbContext)  where T : DbContext, new()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(stream))
                {
                    dbContext = dbContext ?? new T();
                    System.Data.Entity.Infrastructure.EdmxWriter.WriteEdmx(dbContext, writer);
                }
                stream.Position = 0;
                using (var reader = XmlReader.Create(stream))
                {
                    return EdmxReader.Parse(reader);
                }
            }
        }

        /// <summary>
        /// Builds an Entity Data Model (EDM) from a <see cref="DbContext"/> created using Model-First.
        /// Use <see cref="GetCodeFirstEdm"/> for a Code-First DbContext.
        /// </summary>
        /// <typeparam name="T">Type of the source <see cref="DbContext"/></typeparam>
        /// <returns>An XML <see cref="IEdmModel"/>.</returns>
        /// <example>
        /// <![CDATA[
        /// /* In the WebApiConfig.cs */
        /// config.Routes.MapODataRoute(
        ///     routeName: "odata", 
        ///     routePrefix: "odata", 
        ///     model: EdmBuilder.GetModelFirstEdm<ModelFirstDbContext>(), 
        ///     batchHandler: new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer)
        ///     );
        /// ]]>
        /// </example>
        public static IEdmModel GetModelFirstEdm<T>() where T : DbContext, new()
        {
            return GetModelFirstEdm(new T());
        }

        /// <summary>
        /// Extension method builds an Entity Data Model (EDM) from a <see cref="DbContext"/> created using Model-First. 
        /// Use <see cref="GetCodeFirstEdm"/> for a Code-First DbContext.
        /// </summary>
        /// <typeparam name="T">Type of the source <see cref="DbContext"/></typeparam>
        /// <param name="dbContext">Concrete <see cref="DbContext"/> to use for EDM generation.</param>
        /// <returns>An XML <see cref="IEdmModel"/>.</returns>
        /// <remarks>
        /// Inspiration and code for this method came from the following gist
        /// which reates the metadata from an Edmx file:
        /// https://gist.github.com/dariusclay/8673940
        /// </remarks>
        /// <example>
        /// <![CDATA[
        /// /* In the WebApiConfig.cs */
        /// config.Routes.MapODataRoute(
        ///     routeName: "odata", 
        ///     routePrefix: "odata", 
        ///     model: EdmBuilder.GetModelFirstEdm<CustomDbContext>(), 
        ///     batchHandler: new DefaultODataBatchHandler(GlobalConfiguration.DefaultServer)
        ///     );
        /// ]]>
        /// </example>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Usage", "CA2202:Do not dispose objects multiple times" )]
        public static IEdmModel GetModelFirstEdm<T>(this T dbContext) where T : DbContext, new()
        {
            dbContext = dbContext ?? new T();
            using (var csdlStream = GetCsdlStreamFromMetadata(dbContext))
            {
                using (var reader = XmlReader.Create(csdlStream))
                {
                    IEdmModel model;
                    IEnumerable<EdmError> errors;
                    if (!CsdlReader.TryParse(new[] { reader }, out model, out errors))
                    {
                        foreach (var e in errors)
                            Debug.Fail(e.ErrorCode.ToString("F"), e.ErrorMessage);
                    }
                    return model;
                }
            }
        }

        static Stream GetCsdlStreamFromMetadata(IObjectContextAdapter context)
        {
            // Get connection string builder
            var connectionStringBuilder = new EntityConnectionStringBuilder(context.ObjectContext.Connection.ConnectionString);

            // Get the regex match from metadata property of the builder
            var match = Regex.Match(connectionStringBuilder.Metadata, METADATACSDLPATTERN);

            // Get the resource name
            var resourceName = match.Groups[0].Value;

            // Get context assembly
            var assembly = Assembly.GetAssembly(context.GetType());

            // Return the csdl resourcey
            return assembly.GetManifestResourceStream(resourceName);
        }

        // Metadata pattern to find conceptual model name
        const string METADATACSDLPATTERN = "((\\w+\\.)+csdl)";
    }
}

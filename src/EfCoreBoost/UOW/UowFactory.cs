// Copyright © 2026 Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// -----------------------------------------------------------------------------
// EfCore.Boost – Unit of Work Factory Helpers
//
// Helper types that simplify creation of Unit of Work implementations backed
// by SecureContextFactory.
//
// Enables Unit of Work definitions like:
//     public sealed partial class UOWCore(IConfiguration cfg, string cfgName) : UowFactory<DbCore>(cfg, cfgName);
// Instead of repeating context-creation logic:
//     public partial class UOWCore(IConfiguration cfg, string cfgName) : DbUow<DbCore>( () => SecureContextFactory.CreateDbContext<DbCore>(cfg, cfgName));
//
// DbContext creation policy is centralized and reusable, while Unit of Work classes remain compact and declarative.
// -----------------------------------------------------------------------------
using EfCore.Boost.UOW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;

namespace EfCore.Boost.UOW
{
    /// <summary>
    /// Defines a factory responsible for creating <see cref="DbContext"/> instances
    /// used by a Unit of Work.
    ///
    /// The factory abstracts context creation policy, including:
    /// - provider selection
    /// - secure connection handling
    /// - configuration-based initialization
    ///
    /// Unit of Work implementations depend on this interface rather than
    /// constructing DbContext instances directly.
    /// </summary>
    /// <typeparam name="TCtx">Concrete DbContext type.</typeparam>
    public interface IUowFactory<TCtx> where TCtx : DbContext
    {
        TCtx Create();
    }

    /// <summary>
    /// Base Unit of Work implementation that obtains its DbContext
    /// from a factory.
    ///
    /// This type centralizes DbContext creation logic and keeps individual
    /// Unit of Work classes free of provider- and security-specific concerns.
    ///
    /// Two construction paths are supported:
    /// - Using a custom <see cref="IUowFactory{TCtx}"/> (advanced or testing scenarios)
    /// - Using configuration-based creation via the built-in secure factory
    /// </summary>
    /// <typeparam name="TCtx">Concrete DbContext type.</typeparam>
    public abstract class UowFactory<TCtx> : DbUow<TCtx> where TCtx : DbContext
    {
        protected IUowFactory<TCtx> Factory { get; }

        /// <summary>
        /// Creates a Unit of Work using an explicit DbContext factory.
        /// Intended for advanced scenarios such as testing, pooling,
        /// or custom context creation strategies.
        /// </summary>
        protected UowFactory(IUowFactory<TCtx> factory) : base(factory.Create)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Creates a Unit of Work using the default secure, configuration-based
        /// DbContext factory.
        ///
        /// This is the standard construction path used by most applications.
        /// </summary>
        /// <param name="cfg">Application configuration root.</param>
        /// <param name="cfgName">
        /// Name of the database connection entry as defined in configuration.
        /// </param>
        protected UowFactory(IConfiguration cfg, string cfgName) : this(new SecureCfgUowFactory<TCtx>(cfg, cfgName))
        {
        }
    }

    /// <summary>
    /// Default configuration-based factory that creates DbContext instances
    /// using <c>SecureContextFactory</c>.
    ///
    /// This factory encapsulates provider selection, secure connection
    /// handling, and configuration lookup.
    /// </summary>
    /// <typeparam name="TCtx">Concrete DbContext type.</typeparam>
    public sealed class SecureCfgUowFactory<TCtx>(IConfiguration cfg, string cfgName) : IUowFactory<TCtx>  where TCtx : DbContext
    {
        public TCtx Create() => SecureContextFactory.CreateDbContext<TCtx>(cfg, cfgName);
    }
}

// Testing Frameworks
global using Xunit;
global using FluentAssertions;
global using Moq;
global using Xunit.Abstractions;

// .NET Core
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using System.Security.Claims;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.Logging;

// MediatR
global using MediatR;

// Cart Application
global using Cart.Application.Contracts.Persistence;
global using Cart.Application.Contracts.External;
global using Cart.Application.DTos.External;
global using Cart.Application.Features.Carts.Commands;

// Cart Domain
global using Cart.Domain;
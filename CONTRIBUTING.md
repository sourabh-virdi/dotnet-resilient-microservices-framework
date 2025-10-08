# 🤝 Contributing to Resilient Microservices Framework

Thank you for your interest in contributing! This guide will help you get started with contributing to the Resilient Microservices Framework.

## 📋 Table of Contents
- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Contributing Process](#contributing-process)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Documentation](#documentation)
- [Community](#community)

## 🌟 Code of Conduct

This project adheres to a [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

## 🚀 Getting Started

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- [Git](https://git-scm.com/downloads)
- Code editor ([Visual Studio](https://visualstudio.microsoft.com/), [VS Code](https://code.visualstudio.com/), or [JetBrains Rider](https://www.jetbrains.com/rider/))

### Ways to Contribute
- 🐛 **Bug Reports** - Report issues you encounter
- 💡 **Feature Requests** - Suggest new features or improvements
- 📝 **Documentation** - Improve documentation and examples
- 🧪 **Testing** - Add test coverage or improve existing tests
- 💻 **Code Contributions** - Fix bugs or implement new features
- 🎨 **UI/UX** - Improve user experience in sample applications
- 📦 **Framework Extensions** - Add new resilience patterns or integrations

## 💻 Development Setup

### 1. Fork and Clone
```bash
# Fork the repository on GitHub, then clone your fork
git clone https://github.com/sourabh-virdi/dotnet-resilient-microservices-framework.git
cd dotnet-resilient-microservices-framework

# Add upstream remote
git remote add upstream https://github.com/original-owner/dotnet-resilient-microservices-framework.git
```

### 2. Environment Setup
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests to ensure everything works
dotnet test

# Start infrastructure for development
docker-compose up -d rabbitmq jaeger
```

### 3. IDE Configuration

#### Visual Studio
- Open `ResilientMicroservices.Framework.sln`
- Install recommended extensions (if prompted)
- Configure code style (`.editorconfig` is included)

#### VS Code
- Install recommended extensions:
  - C# Dev Kit
  - Docker
  - REST Client (for testing .http files)
- Configure workspace settings (`.vscode/settings.json` included)

#### JetBrains Rider
- Open solution file
- Import code style settings (included in `.editorconfig`)

## 🔄 Contributing Process

### 1. Choose an Issue
- Look at [open issues](https://github.com/sourabh-virdi/dotnet-resilient-microservices-framework/issues)
- Check for issues labeled `good first issue` for beginners
- Comment on the issue to indicate you're working on it

### 2. Create a Feature Branch
```bash
# Create and switch to a new branch
git checkout -b feature/your-feature-name

# Or for bug fixes
git checkout -b fix/issue-description
```

### 3. Make Your Changes
- Follow the [coding standards](#coding-standards)
- Write tests for new functionality
- Update documentation if needed
- Ensure all tests pass

### 4. Commit Your Changes
```bash
# Stage your changes
git add .

# Commit with a descriptive message
git commit -m "feat: add circuit breaker metrics collection

- Add metric recording for circuit breaker state changes
- Include test coverage for new metrics
- Update documentation with new metric names

Closes #123"
```

#### Commit Message Format
We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples:**
```
feat(metrics): add Prometheus histogram support
fix(saga): resolve compensation step execution order
docs(readme): update quick start instructions
test(resilience): add circuit breaker integration tests
```

### 5. Push and Create Pull Request
```bash
# Push your branch to your fork
git push origin feature/your-feature-name

# Create a pull request on GitHub
```

### 6. Pull Request Guidelines
- **Title**: Clear and descriptive
- **Description**: Include:
  - What changes were made and why
  - Link to related issues
  - Screenshots/demos if applicable
  - Breaking changes (if any)
- **Tests**: Ensure all tests pass
- **Documentation**: Update relevant documentation

## 📏 Coding Standards

### General Guidelines
- Follow [Microsoft's C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful names for variables, methods, and classes
- Keep methods small and focused (Single Responsibility Principle)
- Add XML documentation for public APIs
- Use async/await appropriately for I/O operations

### Code Style
```csharp
// ✅ Good: Clear naming and structure
public class PaymentServiceImpl : IPaymentService
{
    private readonly ILogger<PaymentServiceImpl> _logger;
    private readonly IMetricsCollector _metrics;

    /// <summary>
    /// Processes a payment request asynchronously
    /// </summary>
    /// <param name="request">The payment request details</param>
    /// <returns>Payment processing result</returns>
    public async Task<ProcessPaymentResponse> ProcessPaymentAsync(ProcessPaymentRequest request)
    {
        _logger.LogInformation("Processing payment for order {OrderId}", request.OrderId);
        
        try
        {
            var result = await ProcessPaymentInternalAsync(request);
            _metrics.IncrementCounter("payments_processed", 1);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", request.OrderId);
            _metrics.IncrementCounter("payment_errors", 1);
            throw;
        }
    }
}
```

### Framework Integration
- Always inject framework services through DI
- Use the provided abstractions (`IMetricsCollector`, `IDistributedTracing`, etc.)
- Follow the established patterns in existing services

### Configuration
- Use the `ResilientMicroservices` configuration section
- Provide sensible defaults
- Support environment variable overrides

## 🧪 Testing Guidelines

### Test Structure
```bash
tests/
├── ResilientMicroservices.Core.Tests/          # Unit tests for Core
├── PaymentService.Tests/                       # Unit tests for PaymentService
├── InventoryService.Tests/                     # Unit tests for InventoryService
├── ResilientMicroservices.Metrics.Tests/       # Unit tests for Metrics
└── Integration.Tests/                          # Integration tests
```

### Writing Tests
- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test component interactions
- **End-to-End Tests**: Test complete user scenarios

```csharp
[Fact]
public async Task ProcessPaymentAsync_ValidRequest_ShouldReturnSuccessResponse()
{
    // Arrange
    var request = new ProcessPaymentRequest
    {
        OrderId = 123,
        Amount = 99.99m,
        PaymentMethod = "credit_card"
    };

    // Act
    var result = await _paymentService.ProcessPaymentAsync(request);

    // Assert
    result.Should().NotBeNull();
    result.Status.Should().BeOneOf(PaymentStatus.Completed, PaymentStatus.Failed);
    
    _mockMetrics.Verify(m => m.IncrementCounter("payments_processed", 1), Times.Once);
}
```

### Test Requirements
- ✅ All new code must have tests
- ✅ Maintain or improve code coverage
- ✅ Tests must be reliable and deterministic
- ✅ Use descriptive test names
- ✅ Follow Arrange-Act-Assert pattern

### Running Tests
```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test tests/PaymentService.Tests/

# Run tests with filter
dotnet test --filter "Category=Integration"
```

## 📚 Documentation

### What to Document
- **Public APIs**: XML documentation for all public methods
- **Configuration**: Document all configuration options
- **Examples**: Provide usage examples
- **Architecture**: Update diagrams when making structural changes

### Documentation Formats
- **Code Comments**: XML documentation for APIs
- **Markdown**: For guides and documentation files
- **Mermaid Diagrams**: For architecture and flow diagrams
- **OpenAPI/Swagger**: Auto-generated for REST APIs

### Writing Guidelines
- Use clear, concise language
- Include code examples
- Keep documentation up-to-date with code changes
- Consider both beginners and experienced developers

## 🏗️ Architecture Guidelines

### Adding New Framework Components
1. **Core Abstractions**: Define interfaces in `ResilientMicroservices.Core`
2. **Implementation**: Create concrete implementation in appropriate library
3. **Registration**: Add DI registration in `ServiceCollectionExtensions`
4. **Configuration**: Support configuration through `appsettings.json`
5. **Testing**: Comprehensive unit and integration tests
6. **Documentation**: Update architecture docs and examples

### Integration Patterns
- Use dependency injection for all framework components
- Follow the repository pattern for data access
- Implement health checks for external dependencies
- Record metrics for monitoring and observability
- Support distributed tracing for request correlation

## 🔧 Development Workflow

### Daily Development
```bash
# Start your day
git checkout main
git pull upstream main

# Create feature branch
git checkout -b feature/new-feature

# Make changes and test
dotnet build
dotnet test

# Commit and push
git add .
git commit -m "feat: implement new feature"
git push origin feature/new-feature
```

### Before Submitting PR
```bash
# Ensure you're up to date
git fetch upstream
git rebase upstream/main

# Run full test suite
dotnet test

# Check code formatting
dotnet format --verify-no-changes

# Build in release mode
dotnet build --configuration Release
```

## 🎨 UI/UX Guidelines

### Sample Applications
- Keep UI simple and focused on demonstrating framework features
- Use consistent styling across sample applications
- Ensure responsive design for mobile devices
- Include error handling and loading states

### Documentation Site
- Maintain consistent branding and styling
- Ensure good navigation and searchability
- Include interactive examples where possible
- Optimize for both desktop and mobile viewing

## 🚀 Release Process

### Versioning
We follow [Semantic Versioning](https://semver.org/):
- **MAJOR**: Breaking changes
- **MINOR**: New features (backward compatible)
- **PATCH**: Bug fixes (backward compatible)

### Release Checklist
- [ ] All tests pass
- [ ] Documentation is updated
- [ ] CHANGELOG.md is updated
- [ ] Version numbers are bumped
- [ ] Release notes are prepared

## 💬 Community

### Getting Help
- **GitHub Discussions**: For questions and community discussions
- **GitHub Issues**: For bug reports and feature requests
- **Stack Overflow**: Tag questions with `resilient-microservices-framework`

### Communication Guidelines
- Be respectful and inclusive
- Provide context when asking questions
- Help others when you can
- Share your experiences and use cases

### Recognition
We recognize contributors in several ways:
- **Contributors** section in README
- **Release notes** mention significant contributions
- **GitHub badges** for various contribution types

## 📈 Performance Considerations

### Benchmarking
- Use BenchmarkDotNet for performance testing
- Include before/after performance metrics in PRs
- Consider memory allocation and garbage collection impact

### Optimization Guidelines
- Profile before optimizing
- Focus on hot paths and frequently called methods
- Consider async/await overhead
- Minimize allocations in high-throughput scenarios

## 🔒 Security Guidelines

### Security Best Practices
- Never commit secrets or credentials
- Use secure defaults in configuration
- Validate all inputs
- Handle sensitive data appropriately
- Follow OWASP guidelines

### Reporting Security Issues
- **DO NOT** create public issues for security vulnerabilities
- Email security concerns to: [security@example.com]
- Use GitHub's private vulnerability reporting feature

## 🏆 Recognition

### Hall of Fame
We maintain a list of top contributors and recognize their efforts:
- **Core Maintainers**: Long-term project stewards
- **Feature Contributors**: Major feature implementations
- **Documentation Heroes**: Significant documentation improvements
- **Bug Hunters**: Finding and fixing critical issues
- **Community Champions**: Helping others and building community

---

## 🙏 Thank You

Your contributions make this project better for everyone. Whether you're fixing a typo, adding a feature, or helping others in discussions, every contribution matters!

**Happy Coding! 🚀**

---

*For questions about contributing, please reach out via [GitHub Discussions](https://github.com/sourabh-virdi/dotnet-resilient-microservices-framework/discussions) or create an issue.* 
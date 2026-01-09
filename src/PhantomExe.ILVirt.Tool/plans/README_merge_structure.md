# README.md Merge Structure

## Proposed Sections

1. **PhantomExe.ILVirt.Tool** (title)
   - Brief description
   - Badges (if any)

2. **Overview**
   - From README.md lines 3-8

3. **Features**
   - From README.md lines 10-18

4. **Architecture** (expanded)
   - **System Overview** (from ARCHITECTURE.md)
     - High-Level Architecture diagram
   - **Core Components** (from README.md lines 21-29)
   - **Virtualization Process** (from README.md lines 30-49)
   - **Component Architecture** (from ARCHITECTURE.md)
     - Assembly Loader
     - Method Virtualizer
     - Bytecode Format
     - Key Management System
     - VM Runtime System
     - Resource Management
   - **Data Flow** (from ARCHITECTURE.md)
     - Protection Phase Data Flow diagram
     - Runtime Execution Data Flow diagram
   - **Key Management** (detailed from ARCHITECTURE.md)
     - Key Generation Algorithm
     - Key Storage Strategy
     - Key Reconstruction at Runtime
   - **Bytecode Format** (detailed from ARCHITECTURE.md)
     - Instruction Encoding
     - Operand Types
     - Branch Target Resolution
     - Supported CIL to Bytecode Mappings
   - **VM Runtime** (detailed from ARCHITECTURE.md)
     - Stack-Based Interpreter Architecture
     - Interpreter Implementation Details
     - Type Handling
     - Memory Management

5. **Installation**
   - From README.md lines 51-65

6. **Usage** (expanded)
   - **Quick Start** (from USAGE.md)
   - **Command Line Interface** (from USAGE.md)
     - Syntax
     - Parameters
     - Exit Codes
   - **Interactive Mode** (detailed from USAGE.md)
     - Step-by-Step Walkthrough
   - **Auto Mode** (from USAGE.md)
     - Usage
     - Behavior
     - Example Output
   - **Examples** (from USAGE.md)
     - Protect a Console Application
     - Selective Method Virtualization
     - Batch Processing Script
     - Integration with Build Pipeline
   - **Troubleshooting** (from USAGE.md)
     - Common Issues
     - Debug Mode
     - Log Files
   - **Advanced Configuration** (from USAGE.md)
     - Customizing Bytecode Format
     - Key Generation Customization
     - Resource Naming Patterns
     - Runtime Customization
   - **Best Practices** (from USAGE.md)
     - Method Selection Strategy
     - Assembly Preparation
     - Testing Protected Assemblies

7. **Project Structure**
   - From README.md lines 190-211

8. **Dependencies**
   - From README.md lines 213-218

9. **Limitations**
   - From README.md lines 220-238
   - Also include Limitations from USAGE.md lines 400-418

10. **Security Considerations**
    - From README.md lines 240-252
    - Enhanced with ARCHITECTURE.md Security Considerations (Strengths, Weaknesses, Mitigation Strategies)

11. **Development**
    - From README.md lines 254-283

12. **Contributing**
    - From README.md lines 285-290

13. **License**
    - From README.md lines 292-294

14. **Acknowledgments**
    - From README.md lines 296-299

15. **Changelog**
    - Reference to CHANGELOG.md file

## Notes
- Remove duplicate content (e.g., Bytecode Format appears in both README.md and ARCHITECTURE.md)
- Ensure diagrams are preserved (Mermaid)
- Update internal links if any
- Keep CHANGELOG.md separate but reference it
- Delete ARCHITECTURE.md and USAGE.md after successful merge
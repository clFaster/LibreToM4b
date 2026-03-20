# LibreToM4b

A command-line tool to convert audiobooks downloaded with [LibreGrab](https://github.com/PsychedelicPalimpsest/LibbyRip) into M4B audiobook format.

## Features

- **Multi-Format to M4B Conversion**: Concatenates supported audio files (`.mp3`, `.m4a`, `.aac`, `.flac`, `.wav`, `.ogg`, `.opus`, `.wma`, `.ts`) into a single M4B audiobook file
- **Automatic Chapter Detection**: Parses LibreGrab metadata to preserve chapter markers
- **Metadata Preservation**: Extracts and embeds title, author, narrator, and description
- **Fallback Metadata**: Generates metadata from ID3 tags when LibreGrab metadata is unavailable
- **Progress Reporting**: Visual progress bar during conversion
- **Configurable Output**: Custom output directory support

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [FFmpeg](https://ffmpeg.org/download.html) installed and available in PATH

## Installation

```bash
# Clone the repository
git clone https://github.com/clFaster/LibreToM4b.git
cd LibreToM4b

# Build the project
dotnet build

# Run the tool
dotnet run --project LibreToM4b -- convert <input-folder>
```

## Usage

```bash
# Basic usage - converts audiobook to default output folder (Music/LibreToM4b)
LibreToM4b convert <input-folder>

# With custom output folder
LibreToM4b convert <input-folder> --output <output-folder>
LibreToM4b convert <input-folder> -o <output-folder>
```

### Arguments

| Argument | Description |
|----------|-------------|
| `input-folder` | Path to the folder containing supported audio files from LibreGrab |

### Options

| Option | Short | Description |
|--------|-------|-------------|
| `--output` | `-o` | Output folder for the M4B file (default: `Music/LibreToM4b`) |

## Input Folder Structure

The input folder should contain:

```
audiobook-folder/
├── 001.mp3
├── 002.ts
├── ...
└── metadata/
    └── metadata.json    (optional - LibreGrab metadata)
```

## Project Structure

```
LibreToM4b/
├── LibreToM4b.Core/     # Core library with models and services
│   ├── Models/          # Data models (Book, Chapter, etc.)
│   ├── Services/        # Conversion service logic
│   └── Interfaces/      # Service abstractions
└── LibreToM4b/          # CLI application
    └── Commands/        # Command-line command handlers
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

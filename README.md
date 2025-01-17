# Functions Disabler

## Overview

The Functions Disabler is a utility designed to help developers disable all Azure functions locally.
This can be useful for debugging, testing, or temporarily disabling features without removing or changing the code.

## Features

- Disables all Azure functions by going through the project
- Lightweight and fast
- Supports recursive execution spanning multiple function projects

## Constraints or known issues

- The tool will currently only work for functions defined in the actual function app project.
Any indirect import of functions will not generate the expected result.
- Does not work with `local.settings.json` file containing comments.

## Usage

To use the Functions Disabler, run it through the `dotnet` tool

```sh
dotnet run "C:/projects/my-azure-functions-app"
```

## License

This project is licensed under the Unlicense License. See the [LICENSE](LICENSE) file for details.

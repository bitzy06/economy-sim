#!/bin/sh
flatc_bin="${FLATC_PATH:-flatc}"
"$flatc_bin" --csharp -o Generated Protocols/*.fbs

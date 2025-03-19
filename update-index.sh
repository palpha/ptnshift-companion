#!/bin/bash
set -e

files=$(
  aws s3api list-objects \
    --bucket "bergius.org" \
    --prefix "ptnshift/" \
    --profile olaglig \
  | jq -r '.Contents[].Key' \
  | grep -E 'dmg|zip' \
  | sed 's/ptnshift\///'
)

files_arr=()
for file in $files; do
  version=$(echo "$file" | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')
  files_arr+=("$version#$file")
done

sorted_files=$(
  printf "%s\n" "${files_arr[@]}" \
  | sort -t# -k1,1V \
  | cut -d# -f2
)

windows_x64_files_markup=""
windows_arm64_files_markup=""
mac_files_markup=""

file_template="<li><a href=\"https://bergius.org/ptnshift/%s\">%s</a></li>"
for file in $sorted_files; do
  if [[ $file == *dmg ]] || [[ $file == *macOS* ]]; then
    mac_files_markup="$mac_files_markup$(printf "$file_template" "$file" "$file")"
  elif [[ $file == *win-x64* ]]; then
    windows_x64_files_markup="$windows_x64_files_markup$(printf "$file_template" "$file" "$file")"
  elif [[ $file == *win-arm64* ]]; then
    windows_arm64_files_markup="$windows_arm64_files_markup$(printf "$file_template" "$file" "$file")"
  fi
done

# limit width of the markup and center that on screen

cat > index.html <<- EOM
<!DOCTYPE html>
<html>
<head>
    <title>Ptnshift Companion Downloads</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            margin: 2em;
            max-width: 800px;
            margin-left: auto;
            margin-right: auto;
        }
        h1 {
            color: #333;
        }
        h2 {
            color: #666;
        }
        ul {
            list-style-type: none;
            padding: 0;
            margin-bottom: 2em;
        }
        li {
            padding: 0.5em;
            border-bottom: 1px solid #ccc;
        }
        a {
            color: #007bff;
            text-decoration: none;
        }
        a:hover {
            text-decoration: underline;
        }
    </style>
</head>
<body>
    <h1>Ptnshift Companion</h1>

    <h2>macOS</h2>
    <ul>
    $mac_files_markup
    </ul>

    <h2>Windows: x64</h2>
    <ul>
    $windows_x64_files_markup
    </ul>

    <h2>Windows: ARM64</h2>
    <ul>
    $windows_arm64_files_markup
    </ul>

    <h2>Notes</h2>
    <p>
      Windows users, you need to unblock the downloaded ZIP file before extracting it:
      Right-click the ZIP file, select Properties, check Unblock and click OK.
    </p>
    <p>
      For more information, visit <a href="https://www.patreon.com/c/TimExile/shop">Tim Exile's Patreon</a>.
    </p>
    <p>
      Bug reports and PRs are welcome at <a href="https://github.com/palpha/ptnshift-companion">GitHub</a>.
    </p>
</body>
</html>
EOM

aws s3 cp index.html s3://bergius.org/ptnshift/index.html --profile olaglig
rm index.html
#!/bin/bash
set -e

dry_run=false
for arg in "$@"; do
  if [[ $arg == "--dry-run" ]]; then
    dry_run=true
  fi
done

sorted_files=$(
  aws s3api list-objects \
    --bucket "bergius.org" \
    --prefix "ptnshift/" \
    --query 'Contents[?ends_with(Key, `.dmg`) || ends_with(Key, `.zip`)]' \
    --profile olaglig |
    jq -r '[.[]
      | select(.Key | test("_[0-9]+\\.[0-9]+\\.[0-9]+\\.(dmg|zip)$"))
      | {Key, LastModified, version: (.Key | capture("_(?<ver>[0-9]+\\.[0-9]+\\.[0-9]+)") | .ver)}
    ]
    | sort_by(.version | split(".") | map(tonumber))
    | reverse
    | .[] | "\(.Key)\t\(.LastModified)"'
)

windows_x64_files_markup=""
windows_arm64_files_markup=""
mac_files_markup=""

file_template="<li><a href=\"https://bergius.org/%s\">%s</a> <span class=\"last-modified\">(%s)</span></li>"

while IFS=$'\t' read -r file timestamp; do
  pretty_time=$(gdate -u -d "$timestamp" +"%Y-%m-%d %H:%M")
  filename="${file#ptnshift/}"

  if [[ "$file" == *dmg ]] || [[ "$file" == *macOS* ]]; then
    mac_files_markup="${mac_files_markup}$(printf "$file_template" "$file" "$filename" "$pretty_time")"
  elif [[ "$file" == *win-x64* ]]; then
    windows_x64_files_markup="${windows_x64_files_markup}$(printf "$file_template" "$file" "$filename" "$pretty_time")"
  elif [[ "$file" == *win-arm64* ]]; then
    windows_arm64_files_markup="${windows_arm64_files_markup}$(printf "$file_template" "$file" "$filename" "$pretty_time")"
  fi
done <<<"$sorted_files"

# limit width of the markup and center that on screen

cat >index.html <<-EOM
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
    li:first-child a {
      font-weight: bold;
    }
    a {
      color: #007bff;
      text-decoration: none;
    }
    a:hover {
      text-decoration: underline;
    }
    .last-modified {
      font-size: 0.9em;
      color: #999;
    }
  </style>
  <link rel="icon" type="image/png" href="favicon.png">
</head>
<body>
    <h1>PTNSHIFT Companion</h1>

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

# if not dry_run, upload the file to S3
if [ "$dry_run" = false ]; then
  aws s3 cp index.html s3://bergius.org/ptnshift/index.html --profile olaglig
  rm index.html
else
  echo "Dry run: Not uploading index.html to S3."
fi

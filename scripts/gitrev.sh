#!/bin/bash

build=0
rev=$(($GITHUB_RUN_NUMBER+60))
ver=2.0.${rev}.${build}
commit=$GITHUB_SHA
branch=$GITHUB_REF
is_production() { [[ $branch == "refs/heads/main" || $branch == *"prod"* ]]; }
if is_production; then
build_for=production
is_prod_tf=true
else
build_for=development
is_prod_tf=false
fi
echo "Version: ${ver} (Rev=${rev}, Build=${build}, Commit=${commit}, Branch=${branch})"

echo "ASM_BUILD=$rev" >> $GITHUB_ENV
echo "ASM_VER=$ver" >> $GITHUB_ENV
echo "ASM_COMMIT=$commit" >> $GITHUB_ENV
echo "ASM_BUILD_FOR=$build_for" >> $GITHUB_ENV

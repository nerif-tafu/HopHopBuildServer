from setuptools import setup, find_packages

setup(
    name="hophop-build-server",
    package_dir={"": "src"},
    packages=find_packages(where="src"),
    include_package_data=True,
    package_data={
        "hophop.web_server": [
            "templates/*",
            "static/*",
            "static/css/*",
            "static/js/*"
        ],
    },
) 
###
"""
Backend/setup.py
"""
###

from setuptools import setup, find_packages

setup(
    name="AUGV_Backend",
    version="0.3.0",
    packages=find_packages(),
    include_package_data=True,
    install_requires=open("requirements.txt").read().splitlines(),
    entry_points={
        "console_scripts": [
            "AUGV=webapp.__main__:main"
        ]
    },
)
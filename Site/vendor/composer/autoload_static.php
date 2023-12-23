<?php

// autoload_static.php @generated by Composer

namespace Composer\Autoload;

class ComposerStaticInit49760f5a39ccaa8600b88f06b4cfa48e
{
    public static $prefixLengthsPsr4 = array (
        'P' => 
        array (
            'Perlite\\' => 8,
        ),
    );

    public static $prefixDirsPsr4 = array (
        'Perlite\\' => 
        array (
            0 => __DIR__ . '/../..' . '/.src',
        ),
    );

    public static $prefixesPsr0 = array (
        'P' => 
        array (
            'Parsedown' => 
            array (
                0 => __DIR__ . '/..' . '/erusev/parsedown',
            ),
        ),
    );

    public static $classMap = array (
        'Composer\\InstalledVersions' => __DIR__ . '/..' . '/composer/InstalledVersions.php',
    );

    public static function getInitializer(ClassLoader $loader)
    {
        return \Closure::bind(function () use ($loader) {
            $loader->prefixLengthsPsr4 = ComposerStaticInit49760f5a39ccaa8600b88f06b4cfa48e::$prefixLengthsPsr4;
            $loader->prefixDirsPsr4 = ComposerStaticInit49760f5a39ccaa8600b88f06b4cfa48e::$prefixDirsPsr4;
            $loader->prefixesPsr0 = ComposerStaticInit49760f5a39ccaa8600b88f06b4cfa48e::$prefixesPsr0;
            $loader->classMap = ComposerStaticInit49760f5a39ccaa8600b88f06b4cfa48e::$classMap;

        }, null, ClassLoader::class);
    }
}
